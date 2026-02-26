using System.Diagnostics;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using PoC.Observability;
using PoC.Observability.Extensions;
using PoC.Populator.Configuration;
using PoC.Populator.Domain.Models;
using PoC.Populator.Domain.Services;
using PoC.Populator.Infrastructure;
using PoC.Shared.Events;
using PoC.Shared.Models;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

#nullable enable

namespace PoC.Populator.Functions;

public partial class PopulatorWorkerFunction : IAsyncDisposable
{
    public IServiceProvider Services => _hostLazy.Value.Services;

    public async ValueTask DisposeAsync()
    {
        if (_hostLazy.IsValueCreated)
        {
            if (_hostLazy.Value is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else
                _hostLazy.Value.Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Received null event or records")]
    private partial void LogNullEvent();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Received empty SQS Message Body.")]
    private partial void LogEmptyBody();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Received SQS Message Body: {body}")]
    private partial void LogReceivedBody(string body);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Unwrapped SNS Notification. Inner Body: {body}")]
    private partial void LogUnwrappedBody(string body);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialize message body.")]
    private partial void LogDeserializationFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deserialized job is null")]
    private partial void LogNullJob();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Received message is not a valid PopulationJob (Target is missing). Body: {body}")]
    private partial void LogInvalidJob(string body);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing job: Create {batchSize} records for {target} (Min: {min}, Max: {max})")]
    private partial void LogProcessingJob(int batchSize, string target, int? min, int? max);

    [LoggerMessage(Level = LogLevel.Information, Message = "Strategy {strategy} generated {count} items.")]
    private partial void LogStrategyGenerated(string strategy, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown item type generated: {itemType}")]
    private partial void LogUnknownItemType(string itemType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully processed job. Generated and published {publishedCount} events for target {target}.")]
    private partial void LogJobSuccess(int publishedCount, string target);

    [LoggerMessage(Level = LogLevel.Information, Message = "{message}")]
    private partial void LogGenericInfo(string message);

    // Phase 2: Static Host Initialization
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    
    private static readonly Lazy<IHost> _hostLazy = new(
        () =>
        {
            var builder = Host.CreateApplicationBuilder();

            // 1. Observability (Logs, Metrics, Tracing)
            builder.AddPoCObservability(ObservabilityConstants.PopulatorWorkerActivitySourceName, "1.0.0");

            // 2. AWS Services
            builder.Services.AddAWSService<IAmazonSimpleNotificationService>();
            
            // Reuse Infrastructure DI for Options and other shared services
            builder.Services.AddPopulatorInfrastructure(builder.Configuration);

            // 3. HTTP Client with Resilience
            builder.Services.AddHttpClient("MaterialsClient", (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<ServiceOptions>>().Value;
                var materialsUrl = opts.MaterialsApiUrl;
                
                if (!materialsUrl.EndsWith('/'))
                {
                    materialsUrl += "/";
                }
                
                client.BaseAddress = new Uri(materialsUrl);
            })
            .AddStandardResilienceHandler(); // Policies for Retries/CircuitBreaker

            var host = builder.Build();
            host.Start();
            return host;
        },
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Phase 3: Activity Source for Manual Tracing
    private static readonly ActivitySource _activitySource = new(ObservabilityConstants.PopulatorWorkerActivitySourceName);

    private static IHost HostInstance => _hostLazy.Value;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PopulatorWorkerFunction> _logger;
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _topicArn;

    public PopulatorWorkerFunction()
    {
        var host = HostInstance;
        _logger = host.Services.GetRequiredService<ILogger<PopulatorWorkerFunction>>();
        _snsClient = host.Services.GetRequiredService<IAmazonSimpleNotificationService>();
        _httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
        _serviceProvider = host.Services;
        
        var awsOptions = host.Services.GetRequiredService<IOptions<AwsOptions>>().Value;
        _topicArn = awsOptions.SnsTopicArn;
    }

    // Constructor for testing
    public PopulatorWorkerFunction(
        IAmazonSimpleNotificationService snsClient, 
        IHttpClientFactory httpClientFactory,
        ILogger<PopulatorWorkerFunction> logger,
        IServiceProvider serviceProvider,
        string topicArn)
    {
        _snsClient = snsClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _topicArn = topicArn;
    }

#pragma warning disable VSTHRD200
    public async Task FunctionHandler(SQSEvent ev, ILambdaContext context)
    {
        // We use manual activity creation per message in ProcessMessageAsync
        // to handle SQS Batch propagation correctly.
        // A top-level span for the batch can be added here if needed, 
        // but AWSLambdaWrapper is not compatible with typed SQSEvent.
        
        using var activity = _activitySource.StartActivity("ProcessBatch", ActivityKind.Server);
        activity?.SetTag("faas.execution", context.AwsRequestId);
        
        await ProcessEvent();

        async Task ProcessEvent()
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
    }

    private async Task ProcessMessageAsync(SQSEvent.SQSMessage message)
    {
        // Phase 3: Extract Parent Trace Context
        var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, message.MessageAttributes, (attributes, key) =>
        {
            if (attributes.TryGetValue(key, out var value))
            {
                return new[] { value.StringValue };
            }
            return Enumerable.Empty<string>();
        });

        // Start a new Activity linked to the parent context
        using var activity = _activitySource.StartActivity("ProcessSQSMessage", ActivityKind.Consumer, parentContext.ActivityContext);

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
                            DateTime.UtcNow);
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
                        try
                        {
                            if (t.IsCompletedSuccessfully && t.Result != null)
                            {
                                if (t.Result.Successful != null)
                                {
                                    Interlocked.Add(ref publishedCount, t.Result.Successful.Count);
                                }
                                
                                if (t.Result.Failed != null && t.Result.Failed.Count > 0)
                                {
                                    _logger.LogError("Failed to publish {FailedCount} messages in a batch.", t.Result.Failed.Count);
                                }
                            }
                            else if (t.IsFaulted)
                            {
                                _logger.LogError(t.Exception, "Error publishing batch.");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error handling publish batch result.");
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
