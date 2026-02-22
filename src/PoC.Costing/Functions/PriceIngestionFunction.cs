using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using PoC.Costing.Domain.Interfaces;
using PoC.Costing.Infrastructure;
using PoC.Observability.Extensions;
using PoC.Shared.Events;
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
        
        builder.AddPoCObservability("PoC.Costing.PriceIngestion", "1.0.0");

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
        // Filter out non-PriceUpdated events if possible via MessageAttributes
        if (record.MessageAttributes.TryGetValue("EventType", out var eventTypeAttr))
        {
            if (!string.Equals(eventTypeAttr.StringValue, "PriceUpdated", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[PriceIngestion] Ignoring event type: {EventType}", eventTypeAttr.StringValue);
                return;
            }
        }
        else 
        {
            // If MessageAttributes is empty on the SQS record, it might be because the subscription didn't forward them.
            // Or we need to look into the Body -> MessageAttributes (if raw delivery is disabled)
            // But let's check the body first.
        }

        using var doc = JsonDocument.Parse(record.Body);
        var root = doc.RootElement;

        // Try to get MessageAttributes from the SNS body if not present in SQS record attributes
        // Standard SNS to SQS JSON format: "MessageAttributes": { "Key": { "Type": "String", "Value": "..." } }
        if (root.TryGetProperty("MessageAttributes", out var msgAttrs) && 
            msgAttrs.TryGetProperty("EventType", out var eventTypeProp) &&
            eventTypeProp.TryGetProperty("Value", out var eventTypeValue))
        {
             var eventType = eventTypeValue.GetString();
             if (!string.Equals(eventType, "PriceUpdated", StringComparison.OrdinalIgnoreCase))
             {
                 _logger.LogInformation("[PriceIngestion] Ignoring event type (from body): {EventType}", eventType);
                 return;
             }
        }

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
