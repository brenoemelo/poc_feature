using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using PoC.Shared.Events;
using PoC.Shared.Models;
using PoC.Shared.Services;

namespace PoC.Populator;

public class PopulatorWorkerFunction
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly IPopulationStrategy _strategy;
    private readonly string _topicArn;

    public PopulatorWorkerFunction()
    {
        var serviceUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        if (string.IsNullOrEmpty(serviceUrl))
        {
            var localStackHost = Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME") ?? "localhost";
            var edgePort = Environment.GetEnvironmentVariable("EDGE_PORT") ?? "4566";
            serviceUrl = $"http://{localStackHost}:{edgePort}";
        }

        var config = new AmazonSimpleNotificationServiceConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = "us-east-1"
        };
        _snsClient = new AmazonSimpleNotificationServiceClient(config);
        
        _strategy = new MaterialPopulationStrategy();
        // Assuming TOPIC ARN is standard for LocalStack or passed via env var
        _topicArn = Environment.GetEnvironmentVariable("SNS_TOPIC_ARN") ?? "arn:aws:sns:us-east-1:000000000000:material-events";
    }

    public PopulatorWorkerFunction(IAmazonSimpleNotificationService snsClient, IPopulationStrategy strategy, string topicArn)
    {
        _snsClient = snsClient;
        _strategy = strategy;
        _topicArn = topicArn;
    }

#pragma warning disable VSTHRD200
    public async Task FunctionHandler(SQSEvent ev, ILambdaContext context)
#pragma warning restore VSTHRD200
    {
        foreach (var message in ev.Records)
        {
            try
            {
                var job = JsonSerializer.Deserialize<PopulationJob>(message.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (job == null) continue;

                context.Logger.LogInformation($"Processing job: Create {job.BatchSize} records for {job.Target}");

                var items = _strategy.Generate(job.BatchSize);
                
                var tasks = new List<Task>();

                // Publish each item as an event
                foreach (var item in items)
                {
                    var materialEvent = new MaterialCreatedEvent { Material = item };
                    var publishRequest = new PublishRequest
                    {
                        TopicArn = _topicArn,
                        Message = JsonSerializer.Serialize(materialEvent),
                        MessageAttributes = new Dictionary<string, MessageAttributeValue>
                        {
                            { "EventType", new MessageAttributeValue { DataType = "String", StringValue = "MaterialCreated" } }
                        }
                    };
                    
                    tasks.Add(_snsClient.PublishAsync(publishRequest));

                    // Batch tasks to avoid overwhelming the client/network
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

                context.Logger.LogInformation($"Successfully published {job.BatchSize} events.");
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error processing message {message.MessageId}: {ex.Message}");
                throw; 
            }
        }
    }
}
