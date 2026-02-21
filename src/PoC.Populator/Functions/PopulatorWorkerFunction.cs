using System.Diagnostics;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PoC.Populator.Domain.Services;
using PoC.Populator.Infrastructure;
using PoC.Shared.Events;
using PoC.Shared.Models;
using System.Text.Json;

#nullable enable

namespace PoC.Populator.Functions;

public class PopulatorWorkerFunction
{
    // Phase 2: Static Host Initialization
    private static readonly Lazy<IHost> _hostLazy = new(
        () =>
        {
            var builder = Host.CreateApplicationBuilder();

            // 1. Observability (Logs, Metrics, Tracing)

            // 2. AWS Services
            builder.Services.AddAWSService<IAmazonSimpleNotificationService>();
            
            // Reuse Infrastructure DI for Options and other shared services
            builder.Services.AddPopulatorInfrastructure(builder.Configuration);

            // 3. HTTP Client with Resilience
#pragma warning disable S1075 // URIs should not be hardcoded
            var materialsUrl = Environment.GetEnvironmentVariable("MATERIALS_API_URL") 
                               ?? "http://localhost:4566/restapis/material-api/prod/_user_request_";
            
            if (!materialsUrl.EndsWith('/'))
            {
                materialsUrl += "/";
            }
#pragma warning restore S1075 // URIs should not be hardcoded
            
            builder.Services.AddHttpClient("MaterialsClient", client =>
            {
                client.BaseAddress = new Uri(materialsUrl);
            })
            .AddStandardResilienceHandler(); // Policies for Retries/CircuitBreaker

            var host = builder.Build();
            host.Start();
            return host;
        },
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Phase 3: Activity Source for Manual Tracing
    private static readonly ActivitySource _activitySource = new("PoC-Populator-Worker");

    private static IHost HostInstance => _hostLazy.Value;

    private readonly ILogger<PopulatorWorkerFunction> _logger;
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<PopulatorOptions> _options;
    private readonly string _topicArn;

    public PopulatorWorkerFunction()
    {
        var host = HostInstance;
        _logger = host.Services.GetRequiredService<ILogger<PopulatorWorkerFunction>>();
        _snsClient = host.Services.GetRequiredService<IAmazonSimpleNotificationService>();
        _httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
        _options = host.Services.GetRequiredService<IOptions<PopulatorOptions>>();
        
        _topicArn = Environment.GetEnvironmentVariable("SNS_TOPIC_ARN") 
                    ?? "arn:aws:sns:us-east-1:000000000000:material-events";
    }

    // Constructor for testing
    public PopulatorWorkerFunction(
        IAmazonSimpleNotificationService snsClient, 
        IHttpClientFactory httpClientFactory,
        ILogger<PopulatorWorkerFunction> logger,
        IOptions<PopulatorOptions> options,
        string topicArn)
    {
        _snsClient = snsClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options;
        _topicArn = topicArn;
    }

#pragma warning disable VSTHRD200
    public async Task FunctionHandler(SQSEvent ev, ILambdaContext context)
#pragma warning restore VSTHRD200
    {
        if (_logger == null)
        {
            Console.WriteLine("CRITICAL: Logger is not initialized!");
            return;
        }

        if (ev == null || ev.Records == null)
        {
            _logger.LogWarning("Received null event or records");
            return;
        }

        // We use our own Logger, but we can also log to Lambda Context if needed.
        // For consistency, we rely on standard logging which writes to Console (captured by CloudWatch/LocalStack).
        foreach (var message in ev.Records)
        {
        // Phase 3: Extract Parent Trace Context
        var parentContext = ExtractParentContext(message);

        // Start a new Activity linked to the parent context
        using var activity = _activitySource.StartActivity("ProcessSQSMessage", ActivityKind.Consumer, parentContext);

        // Add tags to the activity
        activity?.SetTag("messaging.system", "aws.sqs");
        activity?.SetTag("messaging.destination", "populator-queue");
        activity?.SetTag("messaging.message_id", message.MessageId);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
            ["ReceiptHandle"] = message.ReceiptHandle ?? string.Empty,
            ["TraceId"] = activity?.TraceId.ToString() ?? string.Empty,
            ["SpanId"] = activity?.SpanId.ToString() ?? string.Empty
        });

        try
        {
            if (string.IsNullOrWhiteSpace(message.Body))
            {
                _logger.LogWarning("Received empty SQS Message Body.");
                continue;
            }

            // Phase 1: Reduce Log Noise (Info -> Debug)
            _logger.LogDebug("Received SQS Message Body: {Body}", message.Body);

            string incomingMessageBody = message.Body;

            // Attempt to unwrap SNS Notification
            try 
            {
                using var doc = JsonDocument.Parse(incomingMessageBody);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("Type", out var type) && 
                    type.GetString() == "Notification" &&
                    doc.RootElement.TryGetProperty("Message", out var msg))
                {
                    incomingMessageBody = msg.GetString() ?? incomingMessageBody;
                    _logger.LogDebug("Unwrapped SNS Notification. Inner Body: {Body}", incomingMessageBody);
                }
            }
            catch (JsonException)
            {
                // Not a JSON object or malformed, proceed with original body
            }

            PopulationJob? job = null;
            try
            {
                job = JsonSerializer.Deserialize<PopulationJob>(incomingMessageBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize message body.");
                continue;
            }

            if (job == null) 
            {
                _logger.LogWarning("Deserialized job is null");
                continue;
            }

            // Check if it's a valid job (must have Target)
            if (string.IsNullOrEmpty(job.Target))
            {
                _logger.LogWarning("Received message is not a valid PopulationJob (Target is missing). It might be an event from another topic. Body: {Body}", incomingMessageBody);
                continue;
            }

            _logger.LogInformation(
                "Processing job: Create {BatchSize} records for {Target} (Min: {Min}, Max: {Max})",
                job.BatchSize,
                job.Target ?? "NULL",
                job.MinComponents,
                job.MaxComponents);

            var strategy = GetStrategy(job.Target!, _options);
            var client = _httpClientFactory.CreateClient("MaterialsClient");
            
            var popContext = new PopulationContext 
            { 
                HttpClient = client,
                LogError = (msg, ex) => _logger.LogError(ex, msg)
            };

            var items = await strategy.GenerateAsync(job.BatchSize, popContext, job.MinComponents, job.MaxComponents);
            
            var itemList = items.ToList();
            _logger.LogInformation("Strategy {Strategy} generated {Count} items.", job.Target, itemList.Count);

            var tasks = new List<Task>();
            var publishedCount = 0;

            foreach (var item in itemList)
            {
                string messageBody;
                string eventType;

                if (item is MaterialFormulation material)
                {
                    var evt = new MaterialCreatedEvent(material);
                    messageBody = JsonSerializer.Serialize(evt);
                    eventType = "MaterialCreated";
                }
                else if (item is ComponentPriceRequest price)
                {
                    var evt = new PriceUpdatedEvent(
                        price.ComponentName,
                        price.UnitPrice,
                        price.Currency,
                        DateTime.UtcNow);
                    messageBody = JsonSerializer.Serialize(evt);
                    eventType = "PriceUpdated";
                }
                else
                {
                    _logger.LogWarning("Unknown item type generated: {ItemType}", item.GetType().Name);
                    continue;
                }

                var publishRequest = new PublishRequest
                {
                    TopicArn = _topicArn,
                    Message = messageBody,
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        { "EventType", new MessageAttributeValue { DataType = "String", StringValue = eventType } }
                    }
                };
                
                // Phase 1: Reduce Log Noise (Removed inner loop logging)
                tasks.Add(_snsClient.PublishAsync(publishRequest));

                if (tasks.Count >= 50) 
                {
                    await Task.WhenAll(tasks);
                    publishedCount += tasks.Count;
                    tasks.Clear();
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
                publishedCount += tasks.Count;
            }

            _logger.LogInformation("Successfully processed job. Generated and published {PublishedCount} events for target {Target}.", publishedCount, job.Target);
        }
        catch (Exception exc)
        {
            // Log is handled by the runtime or upper layer when rethrowing
            activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
            throw; 
        }
    }
}

    private static ActivityContext ExtractParentContext(SQSEvent.SQSMessage msg)
    {
        if (msg.MessageAttributes != null && 
            msg.MessageAttributes.TryGetValue("traceparent", out var traceParentAttr) &&
            !string.IsNullOrEmpty(traceParentAttr.StringValue) &&
            ActivityContext.TryParse(traceParentAttr.StringValue, null, out var context))
        {
            return context;
        }

        return default;
    }

    private static IPopulationStrategy GetStrategy(string target, IOptions<PopulatorOptions> options)
    {
        return target.ToLowerInvariant() switch
        {
            "materials" or "material" => new MaterialPopulationStrategy(options),
            "prices" or "price" => new PricePopulationStrategy(options),
            "ensure-prices" => new EnsurePricesPopulationStrategy(options),
            _ => throw new ArgumentException($"Unknown target: {target}")
        };
    }
}
