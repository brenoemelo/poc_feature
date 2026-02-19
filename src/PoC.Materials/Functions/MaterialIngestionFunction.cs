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

    public MaterialIngestionFunction()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddPoCObservability("PoC.Materials", "1.0.0");
        builder.Services.AddMaterialsInfrastructure(builder.Configuration);

        var host = builder.Build();

        _repository = host.Services.GetRequiredService<IMaterialRepository>();
        _logger = host.Services.GetRequiredService<ILogger<MaterialIngestionFunction>>();
    }

    public MaterialIngestionFunction(IMaterialRepository repository, ILogger<MaterialIngestionFunction> logger)
    {
        _repository = repository;
        _logger = logger;
    }

#pragma warning disable VSTHRD200
    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
#pragma warning restore VSTHRD200
    {
        var batchResponse = new SQSBatchResponse();

        _logger.LogInformation("[MaterialIngestion] Processing {Count} SQS messages", sqsEvent.Records.Count);

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                await ProcessSqsRecordAsync(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MaterialIngestion] Failed to process record {MessageId}", record.MessageId);
                batchResponse.BatchItemFailures.Add(new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = record.MessageId
                });
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

        var result = await _repository.SaveAsync(materialEvent.Material);

        if (result.IsFailure)
        {
            // Idempotency: version conflict means item already exists — safe to skip.
            if (result.Error.Code == "DynamoDb.Error" && result.Error.Description.Contains("conditional request failed", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "[MaterialIngestion] Material {MaterialId} skipped due to Optimistic Locking conflict (idempotent)",
                    materialEvent.Material.MaterialId);
                return;
            }

            throw new InvalidOperationException($"Failed to ingest material: {result.Error.Code} - {result.Error.Description}");
        }

        _logger.LogInformation("[MaterialIngestion] Successfully ingested material {MaterialId}", materialEvent.Material.MaterialId);
    }
}
