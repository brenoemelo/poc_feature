using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.Mvc;
using PoC.Shared.Models;
using System.Text.Json;

namespace PoC.Populator.Endpoints;

public static class PopulatorEndpoints
{
    public static RouteGroupBuilder MapPopulatorEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandlePopulationRequestAsync)
             .WithName("CreatePopulationJob");

        return group;
    }

    private static async Task<IResult> HandlePopulationRequestAsync(
        [FromBody] PopulationRequest request,
        [FromServices] IAmazonSQS sqsClient,
        [FromServices] ILogger<Program> logger)
    {
        if (request == null || request.Count <= 0)
        {
            return Results.BadRequest(new { message = "Invalid count" });
        }

        if (request.Count > 100000)
        {
            return Results.BadRequest(new { message = "Count exceeds limit of 100,000" });
        }

        int batchSize = 250;
        int totalBatches = (int)Math.Ceiling((double)request.Count / batchSize);
        logger.LogInformation("Splitting {Count} records into {TotalBatches} batches.", request.Count, totalBatches);

        var serviceUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        if (string.IsNullOrEmpty(serviceUrl))
        {
            var localStackHost = Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME") ?? "localhost";
            var edgePort = Environment.GetEnvironmentVariable("EDGE_PORT") ?? "4566";
            serviceUrl = $"http://{localStackHost}:{edgePort}";
        }

        var queueUrl = $"{serviceUrl}/000000000000/populator-queue";

        var sendTasks = new List<Task>();

        for (int i = 0; i < totalBatches; i++)
        {
            int currentBatchSize = (i == totalBatches - 1) ? request.Count - (i * batchSize) : batchSize;

            var job = new PopulationJob
            {
                Target = request.Target,
                BatchSize = currentBatchSize
            };

            var message = new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = JsonSerializer.Serialize(job)
            };

            sendTasks.Add(sqsClient.SendMessageAsync(message));
        }

        await Task.WhenAll(sendTasks);

        return Results.Accepted(value: new
        {
            message = "Population job accepted",
            total_records = request.Count,
            batches_queued = totalBatches
        });
    }
}
