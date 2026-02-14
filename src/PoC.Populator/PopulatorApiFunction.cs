using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.SQS;
using Amazon.SQS.Model;
using PoC.Shared.Models;
using System.Text.Json;
using System.Net;

namespace PoC.Populator;

public class PopulatorApiFunction
{
    private readonly AmazonSQSClient _sqsClient;
    private readonly string _queueUrl;

    public PopulatorApiFunction()
    {
        var serviceUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        if (string.IsNullOrEmpty(serviceUrl))
        {
            var localStackHost = Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME") ?? "localhost";
            var edgePort = Environment.GetEnvironmentVariable("EDGE_PORT") ?? "4566";
            serviceUrl = $"http://{localStackHost}:{edgePort}";
        }

        var config = new AmazonSQSConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = "us-east-1"
        };
        _sqsClient = new AmazonSQSClient(config);
        
        _queueUrl = $"{serviceUrl}/000000000000/populator-queue";
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        try
        {
            if (request.RequestContext.Http.Method.ToUpper() != "POST")
            {
                return CreateResponse(HttpStatusCode.MethodNotAllowed, new { message = "Only POST is allowed" });
            }

            var populationRequest = JsonSerializer.Deserialize<PopulationRequest>(request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (populationRequest == null || populationRequest.Count <= 0)
            {
                return CreateResponse(HttpStatusCode.BadRequest, new { message = "Invalid count" });
            }

            if (populationRequest.Count > 100000)
            {
                 return CreateResponse(HttpStatusCode.BadRequest, new { message = "Count exceeds limit of 100,000" });
            }

            int batchSize = 250; 
            int totalBatches = (int)Math.Ceiling((double)populationRequest.Count / batchSize);
            context.Logger.LogInformation($"Splitting {populationRequest.Count} records into {totalBatches} batches.");

            var sendTasks = new List<Task>();

            for (int i = 0; i < totalBatches; i++)
            {
                int currentBatchSize = (i == totalBatches - 1) ? populationRequest.Count - (i * batchSize) : batchSize;
                
                var job = new PopulationJob 
                { 
                    Target = populationRequest.Target, 
                    BatchSize = currentBatchSize 
                };
                
                var message = new SendMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MessageBody = JsonSerializer.Serialize(job)
                };

                sendTasks.Add(_sqsClient.SendMessageAsync(message));
            }

            await Task.WhenAll(sendTasks);

            return CreateResponse(HttpStatusCode.Accepted, new 
            { 
                message = "Population job accepted", 
                total_records = populationRequest.Count,
                batches_queued = totalBatches 
            });

        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}");
            return CreateResponse(HttpStatusCode.InternalServerError, new { error = ex.Message });
        }
    }

    private APIGatewayHttpApiV2ProxyResponse CreateResponse(HttpStatusCode statusCode, object body)
    {
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = (int)statusCode,
            Body = JsonSerializer.Serialize(body),
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            }
        };
    }
}
