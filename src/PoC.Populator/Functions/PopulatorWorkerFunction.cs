using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using PoC.Populator.Domain.Services;
using PoC.Shared.Events;
using PoC.Shared.Infrastructure.Extensions;
using PoC.Shared.Models;
using System.Text.Json;

namespace PoC.Populator.Functions;

public class PopulatorWorkerFunction
{
    private readonly ILogger<PopulatorWorkerFunction> _logger;
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _topicArn;

    public PopulatorWorkerFunction()
    {
        var builder = Host.CreateApplicationBuilder();

        // 1. Observability (Logs, Metrics, Tracing)
        builder.AddPoCObservability("PoC-Populator-Worker", "1.0.0");

        // 2. AWS Services
        builder.Services.AddAWSService<IAmazonSimpleNotificationService>();
        
        // 3. HTTP Client with Resilience
#pragma warning disable S1075 // URIs should not be hardcoded
        var materialsUrl = Environment.GetEnvironmentVariable("MATERIALS_API_URL") 
                           ?? "http://localhost:4566/restapis/material-api/prod/_user_request_";
#pragma warning restore S1075 // URIs should not be hardcoded
        
        builder.Services.AddHttpClient("MaterialsClient", client =>
        {
            client.BaseAddress = new Uri(materialsUrl);
        })
        .AddStandardResilienceHandler(); // Policies for Retries/CircuitBreaker

        var host = builder.Build();

        _logger = host.Services.GetRequiredService<ILogger<PopulatorWorkerFunction>>();
        _snsClient = host.Services.GetRequiredService<IAmazonSimpleNotificationService>();
        _httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
        
        _topicArn = Environment.GetEnvironmentVariable("SNS_TOPIC_ARN") 
                    ?? "arn:aws:sns:us-east-1:000000000000:material-events";
    }

    // Constructor for testing
    public PopulatorWorkerFunction(
        IAmazonSimpleNotificationService snsClient, 
        IHttpClientFactory httpClientFactory,
        ILogger<PopulatorWorkerFunction> logger,
        string topicArn)
    {
        _snsClient = snsClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _topicArn = topicArn;
    }

#pragma warning disable VSTHRD200
    public async Task FunctionHandler(SQSEvent ev, ILambdaContext context)
#pragma warning restore VSTHRD200
    {
        // We use our own Logger (Serilog), but we can also log to Lambda Context if needed.
        // For consistency, we rely on Serilog which writes to Console (captured by CloudWatch/LocalStack).
        foreach (var message in ev.Records)
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["MessageId"] = message.MessageId,
                ["ReceiptHandle"] = message.ReceiptHandle ?? string.Empty
            });

            try
            {
                var job = JsonSerializer.Deserialize<PopulationJob>(message.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (job == null) continue;

                _logger.LogInformation("Processing job: Create {BatchSize} records for {Target}", job.BatchSize, job.Target);

                var strategy = GetStrategy(job.Target);
                var client = _httpClientFactory.CreateClient("MaterialsClient");
                
                var popContext = new PopulationContext 
                { 
                    HttpClient = client,
                    LogError = (msg, ex) => _logger.LogError(ex, msg)
                };

                var items = await strategy.GenerateAsync(job.BatchSize, popContext, job.MinComponents, job.MaxComponents);
                
                var tasks = new List<Task>();

                foreach (var item in items)
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
                    
                    tasks.Add(_snsClient.PublishAsync(publishRequest));

                    if (tasks.Count >= 50) 
                    {
                        await Task.WhenAll(tasks);
                        tasks.Clear();
                    }
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                }

                _logger.LogInformation("Successfully published {BatchSize} events for target {Target}.", job.BatchSize, job.Target);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId}", message.MessageId);
                throw; 
            }
        }
    }

    private IPopulationStrategy GetStrategy(string target)
    {
        return target.ToLowerInvariant() switch
        {
            "materials" => new MaterialPopulationStrategy(),
            "prices" => new PricePopulationStrategy(),
            "ensure-prices" => new EnsurePricesPopulationStrategy(),
            _ => throw new ArgumentException($"Unknown target: {target}")
        };
    }
}
