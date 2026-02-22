using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PoC.Materials.Domain.Interfaces;
using PoC.Materials.Infrastructure;
using PoC.Observability.Extensions;
using PoC.Shared.Events;
using System.Text.Json;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace PoC.Materials.Functions;

public sealed partial class MaterialIngestionFunction
{
    private readonly IMaterialRepository _repository;
    private readonly ILogger<MaterialIngestionFunction> _logger;
    private readonly MaterialsMetrics _metrics;

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialIngestion] Processing {Count} SQS messages")]
    private partial void LogProcessingBatch(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "[MaterialIngestion] Failed to process record {MessageId}")]
    private partial void LogProcessingError(Exception ex, string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialIngestion] Batch complete. Processed: {Processed}, Failed: {Failed}")]
    private partial void LogBatchComplete(int processed, int failed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[MaterialIngestion] Record {MessageId} has no 'Message' property")]
    private partial void LogMissingMessageProperty(string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[MaterialIngestion] Record {MessageId} has empty message")]
    private partial void LogEmptyMessage(string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialIngestion] Successfully ingested material {MaterialId} ({Name})")]
    private partial void LogMaterialIngested(string materialId, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[MaterialIngestion] Validation failed for material {MaterialId}")]
    private partial void LogValidationFailed(string materialId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[MaterialIngestion] Record {MessageId} has null material")]
    private partial void LogNullMaterial(string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialIngestion] Ingesting material {MaterialName} ({MaterialId})")]
    private partial void LogIngestingMaterial(string materialName, string materialId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[MaterialIngestion] Material {MaterialId} skipped due to Optimistic Locking conflict (idempotent)")]
    private partial void LogMaterialSkippedIdempotent(string materialId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialIngestion] Successfully ingested material {MaterialId}")]
    private partial void LogSuccessfullyIngested(string materialId);

    public MaterialIngestionFunction()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddPoCObservability("PoC.Materials.Ingestion", "1.0.0");
        builder.Services.AddMaterialsInfrastructure(builder.Configuration);

        var host = builder.Build();

        _repository = host.Services.GetRequiredService<IMaterialRepository>();
        _logger = host.Services.GetRequiredService<ILogger<MaterialIngestionFunction>>();
        _metrics = host.Services.GetRequiredService<MaterialsMetrics>();
    }

    public MaterialIngestionFunction(IMaterialRepository repository, ILogger<MaterialIngestionFunction> logger, MaterialsMetrics metrics)
    {
        _repository = repository;
        _logger = logger;
        _metrics = metrics;
    }

#pragma warning disable VSTHRD200
    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
#pragma warning restore VSTHRD200
    {
        var batchResponse = new SQSBatchResponse();

        _logger.LogInformation("[MaterialIngestion] Processing {Count} SQS messages", sqsEvent.Records.Count);

        foreach (var record in sqsEvent.Records)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string status = "success";
            try
            {
                await ProcessSqsRecordAsync(record);
            }
            catch (Exception ex)
            {
                status = "failure";
                _logger.LogError(ex, "[MaterialIngestion] Failed to process record {MessageId}", record.MessageId);
                batchResponse.BatchItemFailures.Add(new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = record.MessageId
                });
            }
            finally
            {
                sw.Stop();
                _metrics.RecordIngestion(status);
                _metrics.RecordProcessingDuration(sw.Elapsed.TotalMilliseconds);
            }
        }

        _logger.LogInformation(
            "[MaterialIngestion] Batch complete. Processed: {Processed}, Failed: {Failed}",
            sqsEvent.Records.Count - batchResponse.BatchItemFailures.Count,
            batchResponse.BatchItemFailures.Count);

        return batchResponse;
    }

    private async Task ProcessSqsRecordAsync(SQSEvent.SQSMessage record)
    {
        using var doc = JsonDocument.Parse(record.Body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("Message", out var messageProperty))
        {
            LogMissingMessageProperty(record.MessageId);
            return;
        }

        var messageJson = messageProperty.GetString();
        if (string.IsNullOrEmpty(messageJson))
        {
            LogEmptyMessage(record.MessageId);
            return;
        }

        var materialEvent = JsonSerializer.Deserialize<MaterialCreatedEvent>( 
            messageJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (materialEvent?.Material is null)
        {
            LogNullMaterial(record.MessageId);
            return;
        }

        LogIngestingMaterial(materialEvent.Material.Name, materialEvent.Material.MaterialId);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _repository.SaveAsync(materialEvent.Material);
        stopwatch.Stop();
        _metrics.RecordProcessingDuration(stopwatch.Elapsed.TotalMilliseconds);

        if (result.IsFailure)
        {
            // Idempotency: version conflict means item already exists — safe to skip.
            if (result.Error.Code == "DynamoDb.Error" && result.Error.Description.Contains("conditional request failed", StringComparison.OrdinalIgnoreCase))
            {
                _metrics.RecordIngestion("skipped_idempotent");
                LogMaterialSkippedIdempotent(materialEvent.Material.MaterialId);
                return;
            }

            _metrics.RecordIngestion("failed");
            throw new InvalidOperationException($"Failed to ingest material: {result.Error.Code} - {result.Error.Description}");
        }

        _metrics.RecordIngestion("success");
        LogSuccessfullyIngested(materialEvent.Material.MaterialId);
    }
}
