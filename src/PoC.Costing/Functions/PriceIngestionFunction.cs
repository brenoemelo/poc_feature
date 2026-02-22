using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PoC.Costing.Domain.Interfaces;
using PoC.Costing.Infrastructure;
using PoC.Shared.Events;
using PoC.Shared.Models;
using System.Text.Json;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace PoC.Costing.Functions;

public sealed partial class PriceIngestionFunction
{
    private readonly ICostingRepository _repository;
    private readonly ILogger<PriceIngestionFunction> _logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "[PriceIngestion] Processing {Count} SQS messages")]
    private partial void LogProcessingBatch(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "[PriceIngestion] Failed to process record {MessageId}")]
    private partial void LogProcessingError(Exception ex, string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[PriceIngestion] Batch complete. Processed: {Processed}, Failed: {Failed}")]
    private partial void LogBatchComplete(int processed, int failed);

    [LoggerMessage(Level = LogLevel.Information, Message = "[PriceIngestion] Ignoring event type: {EventType}")]
    private partial void LogIgnoringEventType(string eventType);

    [LoggerMessage(Level = LogLevel.Information, Message = "[PriceIngestion] Ignoring event type (from body): {EventType}")]
    private partial void LogIgnoringEventTypeFromBody(string eventType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[PriceIngestion] Record {MessageId} has no 'Message' property")]
    private partial void LogRecordNoMessageProperty(string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[PriceIngestion] Record {MessageId} has empty message")]
    private partial void LogRecordEmptyMessage(string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[PriceIngestion] Record {MessageId} has invalid price event")]
    private partial void LogRecordInvalidPriceEvent(string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[PriceIngestion] Ingesting price for {ComponentName}")]
    private partial void LogIngestingPrice(string componentName);

    [LoggerMessage(Level = LogLevel.Information, Message = "[PriceIngestion] Successfully ingested price for {ComponentName}")]
    private partial void LogSuccessfullyIngestedPrice(string componentName);

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

        LogProcessingBatch(sqsEvent.Records.Count);

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                await ProcessSqsRecordAsync(record);
            }
            catch (Exception ex)
            {
                LogProcessingError(ex, record.MessageId);
                batchResponse.BatchItemFailures.Add(new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = record.MessageId
                });
            }
        }

        LogBatchComplete(
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
                LogIgnoringEventType(eventTypeAttr.StringValue);
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
             if (string.IsNullOrEmpty(eventType) || !string.Equals(eventType, "PriceUpdated", StringComparison.OrdinalIgnoreCase))
             {
                 LogIgnoringEventTypeFromBody(eventType ?? "null");
                 return;
             }
        }

        if (!root.TryGetProperty("Message", out var messageProperty))
        {
            LogRecordNoMessageProperty(record.MessageId);
            return;
        }

        var messageJson = messageProperty.GetString();
        if (string.IsNullOrEmpty(messageJson))
        {
            LogRecordEmptyMessage(record.MessageId);
            return;
        }

        var priceEvent = JsonSerializer.Deserialize<PriceUpdatedEvent>(
            messageJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (priceEvent == null || string.IsNullOrEmpty(priceEvent.ComponentName))
        {
            LogRecordInvalidPriceEvent(record.MessageId);
            return;
        }

        LogIngestingPrice(priceEvent.ComponentName);

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

        LogSuccessfullyIngestedPrice(priceEvent.ComponentName);
    }
}
