using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using PoC.Costing.Domain.Interfaces;
using PoC.Costing.Infrastructure;
using PoC.Observability.Extensions;
using PoC.Shared.Events;
using PoC.Shared.Infrastructure.Extensions;
using PoC.Shared.Models;
using System.Text.Json;

namespace PoC.Costing.Functions;

public sealed class PriceIngestionFunction
{
    private readonly ICostingRepository _repository;
    private readonly ILogger<PriceIngestionFunction> _logger;

    public PriceIngestionFunction()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddPoCObservability(o =>
        {
            o.ServiceName = "PoC.Costing";
            o.OtlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        });
        builder.Services.AddCostingInfrastructure(builder.Configuration);

        var host = builder.Build();

        _repository = host.Services.GetRequiredService<ICostingRepository>();
        _logger = host.Services.GetRequiredService<ILogger<PriceIngestionFunction>>();
    }

    public PriceIngestionFunction(ICostingRepository repository, ILogger<PriceIngestionFunction> logger)
    {
        _repository = repository;
        _logger = logger;
    }

#pragma warning disable VSTHRD200
    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
#pragma warning restore VSTHRD200
    {
        var batchResponse = new SQSBatchResponse();

        _logger.LogInformation("[PriceIngestion] Processing {Count} SQS messages", sqsEvent.Records.Count);

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                await ProcessSqsRecordAsync(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PriceIngestion] Failed to process record {MessageId}", record.MessageId);
                batchResponse.BatchItemFailures.Add(new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = record.MessageId
                });
            }
        }

        _logger.LogInformation(
            "[PriceIngestion] Batch complete. Processed: {Processed}, Failed: {Failed}",
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
            _logger.LogWarning("[PriceIngestion] Record {MessageId} has no 'Message' property", record.MessageId);
            return;
        }

        var messageJson = messageProperty.GetString();
        if (string.IsNullOrEmpty(messageJson))
        {
            _logger.LogWarning("[PriceIngestion] Record {MessageId} has empty message", record.MessageId);
            return;
        }

        var priceEvent = JsonSerializer.Deserialize<PriceUpdatedEvent>(
            messageJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (priceEvent == null || string.IsNullOrEmpty(priceEvent.ComponentName))
        {
            _logger.LogWarning("[PriceIngestion] Record {MessageId} has invalid price event", record.MessageId);
            return;
        }

        _logger.LogInformation("[PriceIngestion] Ingesting price for {ComponentName}", priceEvent.ComponentName);

        var request = new ComponentPriceRequest(
            priceEvent.ComponentName,
            priceEvent.UnitPrice,
            "kg",
            priceEvent.Currency);

        var result = await _repository.UpsertPriceAsync(request);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to ingest price: {result.Error.Code} - {result.Error.Description}");
        }

        _logger.LogInformation("[PriceIngestion] Successfully ingested price for {ComponentName}", priceEvent.ComponentName);
    }
}
