using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using PoC.Shared.Models;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace PoC.Lambda;

public class Function
{
    private readonly AmazonDynamoDBClient _dynamoClient;
    private readonly string _tableName = "poc-table";

    public Function()
    {
        // For local simulation, we point to LocalStack
        var serviceUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        if (string.IsNullOrEmpty(serviceUrl))
        {
            var localStackHost = Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME") ?? "localhost";
            var edgePort = Environment.GetEnvironmentVariable("EDGE_PORT") ?? "4566";
            serviceUrl = $"http://{localStackHost}:{edgePort}";
        }

        // If running inside LocalStack docker, localhost might not work,
        // but LocalStack usually handles this via bridge or environment variables.
        // For simplicity in this POC, we check if we are in docker.
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = "us-east-1"
        };
        _dynamoClient = new AmazonDynamoDBClient(config);
    }

    public async Task<string> FunctionHandler(MaterialFormulation input, ILambdaContext context)
    {
        context.Logger.LogInformation($"Processing material: {input.Name} ({input.MaterialId})");

        try
        {
            var json = JsonSerializer.Serialize(input);
            var doc = Document.FromJson(json);

            context.Logger.LogInformation($"Saving to table {_tableName}...");

            var table = Table.LoadTable(_dynamoClient, _tableName);
            await table.PutItemAsync(doc);

            context.Logger.LogInformation("Successfully saved to DynamoDB.");
            return $"Material {input.MaterialId} stored successfully.";
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error saving to DynamoDB: {ex.Message}");
            throw;
        }
    }
}
