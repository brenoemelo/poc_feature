using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using PoC.Shared.Models;
using System.Text.Json;
using System.Net;

namespace PoC.Lambda;

public class QueryFunction
{
    private readonly AmazonDynamoDBClient _dynamoClient;
    private readonly DynamoDBContext _context;
    private readonly string _tableName = "poc-table";

    public QueryFunction()
    {
        var serviceUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        if (string.IsNullOrEmpty(serviceUrl))
        {
            var localStackHost = Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME") ?? "localhost";
            var edgePort = Environment.GetEnvironmentVariable("EDGE_PORT") ?? "4566";
            serviceUrl = $"http://{localStackHost}:{edgePort}";
        }

        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = "us-east-1"
        };
        _dynamoClient = new AmazonDynamoDBClient(config);
        _context = new DynamoDBContext(_dynamoClient);
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation($"HTTP Method: {request.RequestContext.Http.Method}");
        context.Logger.LogInformation($"Path: {request.RequestContext.Http.Path}");

        try
        {
            return request.RequestContext.Http.Method.ToUpper() switch
            {
                "GET" => await HandleGetRequest(request, context),
                _ => CreateResponse(HttpStatusCode.MethodNotAllowed, new { message = "Method not allowed" })
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Unhandled error: {ex.Message}");
            return CreateResponse(HttpStatusCode.InternalServerError, new { error = "Internal server error", detail = ex.Message });
        }
    }

    private async Task<APIGatewayHttpApiV2ProxyResponse> HandleGetRequest(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        // Simple routing based on path
        // Expected paths: /materials or /materials/{id}
        
        var path = request.RequestContext.Http.Path.TrimEnd('/');
        
        if (path == "/materials")
        {
            context.Logger.LogInformation("Listing all materials...");
            var table = Table.LoadTable(_dynamoClient, _tableName);
            var search = table.Scan(new ScanOperationConfig());
            var items = await search.GetRemainingAsync();
            
            var materials = items.Select(item => JsonSerializer.Deserialize<MaterialFormulation>(item.ToJson())).ToList();
            return CreateResponse(HttpStatusCode.OK, materials);
        }

        if (path.StartsWith("/materials/"))
        {
            var id = path.Substring("/materials/".Length);
            context.Logger.LogInformation($"Getting material with ID: {id}");
            
            var table = Table.LoadTable(_dynamoClient, _tableName);
            var item = await table.GetItemAsync(id);
            
            if (item == null)
            {
                return CreateResponse(HttpStatusCode.NotFound, new { message = $"Material {id} not found" });
            }

            var material = JsonSerializer.Deserialize<MaterialFormulation>(item.ToJson());
            return CreateResponse(HttpStatusCode.OK, material);
        }

        return CreateResponse(HttpStatusCode.NotFound, new { message = "Resource not found" });
    }

    private APIGatewayHttpApiV2ProxyResponse CreateResponse(HttpStatusCode statusCode, object body)
    {
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = (int)statusCode,
            Body = JsonSerializer.Serialize(body),
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Access-Control-Allow-Origin", "*" }
            }
        };
    }
}
