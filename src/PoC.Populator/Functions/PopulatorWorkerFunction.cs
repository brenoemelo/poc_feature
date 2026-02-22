using System.Diagnostics;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoC.Populator.Domain.Services;
using PoC.Populator.Infrastructure;
using PoC.Observability.Extensions;
using PoC.Shared.Events;
using PoC.Shared.Models;
using System.Text.Json;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

#nullable enable

namespace PoC.Populator.Functions;

public partial class PopulatorWorkerFunction
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Received null event or records")]
    private partial void LogNullEvent();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Received empty SQS Message Body.")]
    private partial void LogEmptyBody();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Received SQS Message Body: {Body}")]
    private partial void LogReceivedBody(string body);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Unwrapped SNS Notification. Inner Body: {Body}")]
    private partial void LogUnwrappedBody(string body);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialize message body.")]
    private partial void LogDeserializationFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deserialized job is null")]
    private partial void LogNullJob();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Received message is not a valid PopulationJob (Target is missing). Body: {Body}")]
    private partial void LogInvalidJob(string body);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing job: Create {BatchSize} records for {Target} (Min: {Min}, Max: {Max})")]
    private partial void LogProcessingJob(int batchSize, string target, int? min, int? max);

    [LoggerMessage(Level = LogLevel.Information, Message = "Strategy {Strategy} generated {Count} items.")]
    private partial void LogStrategyGenerated(string strategy, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown item type generated: {ItemType}")]
    private partial void LogUnknownItemType(string itemType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully processed job. Generated and published {PublishedCount} events for target {Target}.")]
    private partial void LogJobSuccess(int publishedCount, string target);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Message}")]
    private partial void LogGenericInfo(string message);

    // Phase 2: Static Host Initialization
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    
    private static readonly Lazy<IHost> _hostLazy = new(
        () =>
        {
            var builder = Host.CreateApplicationBuilder();

            // 1. Observability (Logs, Metrics, Tracing)
            builder.AddPoCObservability("PoC.Populator.Worker", "1.0.0");

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

    private readonly IServiceProvider _serviceProvider;

    public PopulatorWorkerFunction()
    {
        var host = HostInstance;
        _logger = host.Services.GetRequiredService<ILogger<PopulatorWorkerFunction>>();
        _snsClient = host.Services.GetRequiredService<IAmazonSimpleNotificationService>();
        _httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
        _options = host.Services.GetRequiredService<IOptions<PopulatorOptions>>();
        _serviceProvider = host.Services;
        
        _topicArn = Environment.GetEnvironmentVariable("SNS_TOPIC_ARN") 
                    ?? "arn:aws:sns:us-east-1:000000000000:material-events";
    }

    // Constructor for testing
    public PopulatorWorkerFunction(
        IAmazonSimpleNotificationService snsClient, 
        IHttpClientFactory httpClientFactory,
        ILogger<PopulatorWorkerFunction> logger,
        IOptions<PopulatorOptions> options,
        IServiceProvider serviceProvider,
        string topicArn)
    {
        _snsClient = snsClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options;
        _serviceProvider = serviceProvider;
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
            LogNullEvent();
            return;
        }

        // Parallel processing of SQS messages
        // We use Task.WhenAll to process all messages concurrently
        var processingTasks = ev.Records.Select(ProcessMessageAsync);
        
        try
        {
            await Task.WhenAll(processingTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing one or more messages in the batch.");
            throw; // Re-throw to let Lambda know (partial batch failure handling might be needed in real prod)
        }
    }

    private async Task ProcessMessageAsync(SQSEvent.SQSMessage message)
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
                LogEmptyBody();
                return;
            }

            // Phase 1: Reduce Log Noise (Info -> Debug)
            LogReceivedBody(message.Body);

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
                    LogUnwrappedBody(incomingMessageBody);
                }
            }
            catch (JsonException)
            {
                // Not a JSON object or malformed, proceed with original body
            }

            PopulationJob? job = null;
            try
            {
                job = JsonSerializer.Deserialize<PopulationJob>(incomingMessageBody, _jsonOptions);
            }
            catch (JsonException ex)
            {
                LogDeserializationFailed(ex);
                return;
            }

            if (job == null) 
            {
                LogNullJob();
                return;
            }

            // Check if it's a valid job (must have Target)
            if (string.IsNullOrEmpty(job.Target))
            {
                LogInvalidJob(incomingMessageBody);
                return;
            }

            LogProcessingJob(job.BatchSize, job.Target, job.MinComponents, job.MaxComponents);

            var strategy = GetStrategy(job.Target!);
            var client = _httpClientFactory.CreateClient("MaterialsClient");
            
            var popContext = new PopulationContext 
            { 
                HttpClient = client,
                LogError = (msg, ex) => _logger.LogError(ex, msg),
                LogInformation = LogGenericInfo
            };

            var items = await strategy.GenerateAsync(job.BatchSize, popContext, job.MinComponents, job.MaxComponents);
            
            var itemList = items.ToList();
            LogStrategyGenerated(job.Target, itemList.Count);

            // Use SNS Batch Publish (Max 10 items per batch)
            var chunks = itemList.Chunk(10);
            var publishedCount = 0;
            var publishTasks = new List<Task>();

            foreach (var chunk in chunks)
            {
                var entries = new List<PublishBatchRequestEntry>();
                foreach (var item in chunk)
                {
                    string messageBody;
                    string eventType;

                    if (item is MaterialFormulation material)
                    {
                        var evt = new MaterialCreatedEvent(material);
                        messageBody = JsonSerializer.Serialize(evt, _jsonOptions);
                        eventType = "MaterialCreated";
                    }
                    else if (item is ComponentPriceRequest price)
                    {
                        var evt = new PriceUpdatedEvent(
                            price.ComponentName,
                            price.UnitPrice,
                            price.Currency,
                            price.EffectiveDate);
                        messageBody = JsonSerializer.Serialize(evt, _jsonOptions);
                        eventType = "PriceUpdated";
                    }
                    else
                    {
                        LogUnknownItemType(item.GetType().Name);
                        continue;
                    }

                    entries.Add(new PublishBatchRequestEntry
                    {
                        Id = Guid.NewGuid().ToString(),
                        Message = messageBody,
                        MessageAttributes = new Dictionary<string, MessageAttributeValue>
                        {
                            { "EventType", new MessageAttributeValue { DataType = "String", StringValue = eventType } }
                        }
                    });
                }

                if (entries.Any())
                {
                    publishTasks.Add(_snsClient.PublishBatchAsync(new PublishBatchRequest
                    {
                        TopicArn = _topicArn,
                        PublishBatchRequestEntries = entries
                    }).ContinueWith(t => 
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            Interlocked.Add(ref publishedCount, t.Result.Successful.Count);
                            if (t.Result.Failed.Count > 0)
                            {
                                _logger.LogError("Failed to publish {FailedCount} messages in a batch.", t.Result.Failed.Count);
                            }
                        }
                        else if (t.IsFaulted)
                        {
                            _logger.LogError(t.Exception, "Error publishing batch.");
                        }
                    }));
                }
            }

            await Task.WhenAll(publishTasks);

            LogJobSuccess(publishedCount, job.Target);
        }
        catch (Exception exc)
        {
            // Log is handled by the runtime or upper layer when rethrowing
            activity?.SetStatus(ActivityStatusCode.Error, exc.Message);
            throw; 
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

    private IPopulationStrategy GetStrategy(string target)
    {
        return target.ToLowerInvariant() switch
        {
            "materials" or "material" => _serviceProvider.GetRequiredService<MaterialPopulationStrategy>(),
            "prices" or "price" => _serviceProvider.GetRequiredService<PricePopulationStrategy>(),
            "ensure-prices" => _serviceProvider.GetRequiredService<EnsurePricesPopulationStrategy>(),
            _ => throw new ArgumentException($"Unknown target: {target}")
        };
    }
}
