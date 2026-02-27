using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using PoC.Populator.Configuration;
using PoC.Populator.Domain.Interfaces;
using PoC.Shared.Common;
using PoC.Populator.Domain.Models;
using PoC.Shared.Models;
using System.Text.Json;

namespace PoC.Populator.Infrastructure.Services;

public sealed partial class PopulationService(
    IAmazonSQS sqsClient,
    IOptions<AwsOptions> awsOptions,
    ILogger<PopulationService> logger) : IPopulationService
{
    private readonly ILogger<PopulationService> _logger = logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "Splitting {Count} records into {TotalBatches} batches.")]
    private partial void LogSplittingRecords(int count, int totalBatches);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create population job")]
    private partial void LogJobCreationFailure(Exception ex);

    public async Task<Result<PopulationJobResponse>> CreateJobAsync(PopulationRequest request)
    {
        try
        {
            int batchSize = 250;
            int totalBatches = (int)Math.Ceiling((double)request.Count / batchSize);
            LogSplittingRecords(request.Count, totalBatches);

            var queueUrl = awsOptions.Value.SqsQueueUrl;

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
                    MessageBody = JsonSerializer.Serialize(job, SerializationDefaults.Options)
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
            LogJobCreationFailure(ex);
            return Result.Failure<PopulationJobResponse>(new Error("Populator.Error", ex.Message));
        }
    }
}
