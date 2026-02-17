using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using PoC.Populator.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Shared.Models;
using System.Text.Json;

namespace PoC.Populator.Infrastructure.Services;

public sealed class PopulationService(
    IAmazonSQS sqsClient,
    IOptions<PopulatorOptions> options,
    ILogger<PopulationService> logger) : IPopulationService
{
    public async Task<Result<PopulationJobResponse>> CreateJobAsync(PopulationRequest request)
    {
        try
        {
            int batchSize = 250;
            int totalBatches = (int)Math.Ceiling((double)request.Count / batchSize);
            logger.LogInformation("Splitting {Count} records into {TotalBatches} batches.", request.Count, totalBatches);

            var queueUrl = options.Value.QueueUrl;

            var sendTasks = new List<Task>();

            for (int i = 0; i < totalBatches; i++)
            {
                int currentBatchSize = (i == totalBatches - 1) ? request.Count - (i * batchSize) : batchSize;

                var job = new PopulationJob(
                    request.Target,
                    currentBatchSize,
                    request.MinComponents,
                    request.MaxComponents);

                var message = new SendMessageRequest
                {
                    QueueUrl = queueUrl,
                    MessageBody = JsonSerializer.Serialize(job)
                };

                sendTasks.Add(sqsClient.SendMessageAsync(message));
            }

            await Task.WhenAll(sendTasks);

            return Result.Success(new PopulationJobResponse(
                "Population job accepted",
                request.Count,
                totalBatches));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create population job");
            return Result.Failure<PopulationJobResponse>(new Error("Populator.Error", ex.Message));
        }
    }
}
