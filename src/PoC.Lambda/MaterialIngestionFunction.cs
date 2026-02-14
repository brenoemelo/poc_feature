using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using PoC.Shared.Models;
using PoC.Shared.Events;
using System.Text.Json;

namespace PoC.Lambda;

public class MaterialIngestionFunction
{
    private readonly AmazonDynamoDBClient _dynamoClient;
    private readonly string _tableName = "poc-table";

    public MaterialIngestionFunction()
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
    }

    public async Task FunctionHandler(SQSEvent ev, ILambdaContext context)
    {
        foreach (var record in ev.Records)
        {
            try
            {
                // SQS message body contains the SNS notification
                using var doc = JsonDocument.Parse(record.Body);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("Message", out var messageProperty))
                {
                   var messageJson = messageProperty.GetString();
                   if (string.IsNullOrEmpty(messageJson)) continue;

                   var materialEvent = JsonSerializer.Deserialize<MaterialCreatedEvent>(messageJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                   
                   if (materialEvent?.Material != null)
                   {
                       await SaveMaterialAsync(materialEvent.Material, context);
                   }
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error processing record {record.MessageId}: {ex.Message}");
                // In a real scenario, we might want to throw to trigger DLQ, but for now we log.
            }
        }
    }

    private async Task SaveMaterialAsync(MaterialFormulation material, ILambdaContext context)
    {
        context.Logger.LogInformation($"Ingesting material: {material.Name} ({material.MaterialId})");
        
        var json = JsonSerializer.Serialize(material);
        var doc = Document.FromJson(json);
        var table = Table.LoadTable(_dynamoClient, _tableName);
        
        await table.PutItemAsync(doc);
    }
}
