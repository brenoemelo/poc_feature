using System.Diagnostics;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PoC.Costing.Domain.Interfaces;
using PoC.Costing.Infrastructure;
using PoC.Observability.Extensions;
using PoC.Shared.Events;
using PoC.Shared.Models;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace PoC.Costing.Functions;

public sealed partial class PriceIngestionFunction : IAsyncDisposable
{
    private static readonly ActivitySource _activitySource = new("PoC.Costing.PriceIngestion");

    private readonly IHost _host;
    public IServiceProvider Services => _host.Services;

    private readonly ICostingRepository _repository;
    private readonly ILogger<PriceIngestionFunction> _logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "[PriceIngestion] Processing {count} SQS messages")]
    private partial void LogProcessingBatch(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "[PriceIngestion] Failed to process record {messageId}")]
    private partial void LogProcessingError(Exception ex, string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[PriceIngestion] Batch complete. Processed: {processed}, Failed: {failed}")]
    private partial void LogBatchComplete(int processed, int failed);

    [LoggerMessage(Level = LogLevel.Information, Message = "[PriceIngestion] Ignoring event type: {eventType}")]
    private partial void LogIgnoringEventType(string eventType);

    [LoggerMessage(Level = LogLevel.Information, Message = "[PriceIngestion] Ignoring event type (from body): {eventType}")]
    private partial void LogIgnoringEventTypeFromBody(string eventType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[PriceIngestion] Record {messageId} has no 'Message' property")]
    private partial void LogRecordNoMessageProperty(string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[PriceIngestion] Record {messageId} has empty message")]
    private partial void LogRecordEmptyMessage(string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[PriceIngestion] Record {messageId} has invalid price event")]
    private partial void LogRecordInvalidPriceEvent(string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[PriceIngestion] Ingesting price for {componentName}")]
    private partial void LogIngestingPrice(string componentName);

    [LoggerMessage(Level = LogLevel.Information, Message = "[PriceIngestion] Successfully ingested price for {componentName}")]
    private partial void LogSuccessfullyIngestedPrice(string componentName);

    public PriceIngestionFunction()
    {
        var builder = Host.CreateApplicationBuilder();
        
        builder.AddPoCObservability("PoC.Costing.PriceIngestion", "1.0.0");

        builder.Services.AddCostingInfrastructure(builder.Configuration);

        _host = builder.Build();
        _host.Start();

        _repository = _host.Services.GetRequiredService<ICostingRepository>();
        _logger = _host.Services.GetRequiredService<ILogger<PriceIngestionFunction>>();
    }

    public PriceIngestionFunction(ICostingRepository repository, ILogger<PriceIngestionFunction> logger)
    {
        _repository = repository;
        _logger = logger;
        _host = null!; // Mocking constructor
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _host?.Dispose();
        }
    }

#pragma warning disable VSTHRD200
    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
#pragma warning restore VSTHRD200
    {
        using var activity = _activitySource.StartActivity("ProcessBatch", ActivityKind.Server);
        activity?.SetTag("faas.execution", context.AwsRequestId);
        activity?.SetTag("messaging.batch.message_count", sqsEvent.Records.Count);

        return await ProcessEvent();

    async Task<SQSBatchResponse> ProcessEvent()
        {
            var batchResponse = new SQSBatchResponse();

            _logger.LogInformation("Processing {Count} records...", sqsEvent.Records.Count);

            foreach (var record in sqsEvent.Records)
            {
                try
                {
                    await ProcessSqsRecordAsync(record);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing SQS record {MessageId}", record.MessageId);
                    batchResponse.BatchItemFailures.Add(new SQSBatchResponse.BatchItemFailure
                    {
                        ItemIdentifier = record.MessageId
                    });
                }
            }

            return batchResponse;
        }
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
        string? messageJson;

        // Check if it is an SNS Envelope (RawMessageDelivery = false)
        if (root.ValueKind == JsonValueKind.Object && 
            root.TryGetProperty("Type", out var typeProp) && 
            typeProp.GetString() == "Notification" &&
            root.TryGetProperty("Message", out var messageProp))
        {
            messageJson = messageProp.GetString();

            // Try to get MessageAttributes from the SNS body if not present in SQS record attributes
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
        }
        else
        {
            // Assume Raw Message (RawMessageDelivery = true)
            messageJson = record.Body;
        }

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
