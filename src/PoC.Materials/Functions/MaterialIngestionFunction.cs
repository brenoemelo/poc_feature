using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PoC.Materials.Domain.Interfaces;
using PoC.Materials.Infrastructure;
using PoC.Observability.Extensions;
using PoC.Shared.Common;
using PoC.Shared.Events;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;
using System.Text.Json;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace PoC.Materials.Functions;

public sealed partial class MaterialIngestionFunction
{
    private readonly IMaterialRepository _repository;
    private readonly ILogger<MaterialIngestionFunction> _logger;
    private readonly MaterialsMetrics _metrics;
    private static readonly ActivitySource _activitySource = new("PoC.Materials.Ingestion");
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MaterialIngestionFunction()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddPoCObservability("PoC.Materials.Ingestion", "1.0.0");
        builder.Services.AddMaterialsInfrastructure(builder.Configuration);

        var host = builder.Build();
        host.Start();

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
        using var activity = _activitySource.StartActivity("ProcessBatch", ActivityKind.Server);
        activity?.SetTag("faas.execution", context.AwsRequestId);
        activity?.SetTag("messaging.batch.message_count", sqsEvent.Records.Count);

        var batchResponse = new SQSBatchResponse();

        LogProcessingBatch(sqsEvent.Records.Count);

        foreach (var record in sqsEvent.Records)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string status = "success";

            // Extract parent context from SQS message attributes
            var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, record.MessageAttributes, (attributes, key) =>
            {
                if (attributes.TryGetValue(key, out var value))
                {
                    return new[] { value.StringValue };
                }
                return Enumerable.Empty<string>();
            });

            // Start a new activity for processing this message, linked to the parent
            using var messageActivity = _activitySource.StartActivity("ProcessMessage", ActivityKind.Consumer, parentContext.ActivityContext);
            messageActivity?.SetTag("messaging.system", "sqs");
            messageActivity?.SetTag("messaging.message_id", record.MessageId);

            try
            {
                await ProcessSqsRecordAsync(record);
            }
            catch (Exception ex)
            {
                status = "failure";
                LogProcessingError(ex, record.MessageId);
                batchResponse.BatchItemFailures.Add(new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = record.MessageId
                });
                messageActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
            finally
            {
                sw.Stop();
                _metrics.RecordIngestion(status);
                _metrics.RecordProcessingDuration(sw.Elapsed.TotalMilliseconds);
            }
        }

        LogBatchComplete(sqsEvent.Records.Count - batchResponse.BatchItemFailures.Count, batchResponse.BatchItemFailures.Count);

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
            _jsonOptions);

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
            if (result.Error == Error.ConditionNotMet)
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

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialIngestion] Processing {count} SQS messages")]
    partial void LogProcessingBatch(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "[MaterialIngestion] Failed to process record {messageId}")]
    partial void LogProcessingError(Exception ex, string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialIngestion] Batch complete. Processed: {processed}, Failed: {failed}")]
    partial void LogBatchComplete(int processed, int failed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[MaterialIngestion] Record {messageId} has no 'Message' property")]
    partial void LogMissingMessageProperty(string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[MaterialIngestion] Record {messageId} has empty message")]
    partial void LogEmptyMessage(string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialIngestion] Successfully ingested material {materialId} ({name})")]
    partial void LogMaterialIngested(string materialId, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[MaterialIngestion] Validation failed for material {materialId}")]
    partial void LogValidationFailed(string materialId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[MaterialIngestion] Record {messageId} has null material")]
    partial void LogNullMaterial(string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialIngestion] Ingesting material {materialName} ({materialId})")]
    partial void LogIngestingMaterial(string materialName, string materialId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[MaterialIngestion] Material {materialId} skipped due to Optimistic Locking conflict (idempotent)")]
    partial void LogMaterialSkippedIdempotent(string materialId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[MaterialIngestion] Successfully ingested material {materialId}")]
    partial void LogSuccessfullyIngested(string materialId);
}
