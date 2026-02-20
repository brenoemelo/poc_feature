using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using PoC.Materials.Domain.Interfaces;
using PoC.Materials.Infrastructure;
using PoC.Observability.Extensions;
using PoC.Shared.Events;
using System.Text.Json;

namespace PoC.Materials.Functions;

public sealed class MaterialIngestionFunction
{
    private readonly IMaterialRepository _repository;
    private readonly ILogger<MaterialIngestionFunction> _logger;
    private readonly MaterialsMetrics _metrics;
    private readonly IHost? _host;

    public MaterialIngestionFunction()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddPoCObservability("PoC.Materials", "1.0.0");
        builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter(MaterialsMetrics.MeterName));
        builder.Services.AddMaterialsInfrastructure(builder.Configuration);

        var host = builder.Build();

        _repository = host.Services.GetRequiredService<IMaterialRepository>();
        _logger = host.Services.GetRequiredService<ILogger<MaterialIngestionFunction>>();
        _metrics = host.Services.GetRequiredService<MaterialsMetrics>();
        _host = host;
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
        try
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
        finally
        {
            _host?.Services.FlushOpenTelemetryProviders();
        }
    }

    private async Task ProcessSqsRecordAsync(SQSEvent.SQSMessage record)
    {
        using var doc = JsonDocument.Parse(record.Body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("Message", out var messageProperty))
        {
            _logger.LogWarning("[MaterialIngestion] Record {MessageId} has no 'Message' property", record.MessageId);
            return;
        }

        var messageJson = messageProperty.GetString();
        if (string.IsNullOrEmpty(messageJson))
        {
            _logger.LogWarning("[MaterialIngestion] Record {MessageId} has empty message", record.MessageId);
            return;
        }

        var materialEvent = JsonSerializer.Deserialize<MaterialCreatedEvent>(
            messageJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (materialEvent?.Material is null)
        {
            _logger.LogWarning("[MaterialIngestion] Record {MessageId} has null material", record.MessageId);
            return;
        }

        _logger.LogInformation(
            "[MaterialIngestion] Ingesting material {MaterialName} ({MaterialId})",
            materialEvent.Material.Name,
            materialEvent.Material.MaterialId);

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
                _logger.LogWarning(
                    "[MaterialIngestion] Material {MaterialId} skipped due to Optimistic Locking conflict (idempotent)",
                    materialEvent.Material.MaterialId);
                return;
            }

            _metrics.RecordIngestion("failed");
            throw new InvalidOperationException($"Failed to ingest material: {result.Error.Code} - {result.Error.Description}");
        }

        _metrics.RecordIngestion("success");
        _logger.LogInformation("[MaterialIngestion] Successfully ingested material {MaterialId}", materialEvent.Material.MaterialId);
    }
}
