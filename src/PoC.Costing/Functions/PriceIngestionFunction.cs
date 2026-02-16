using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using PoC.Costing.Domain.Interfaces;
using PoC.Costing.Infrastructure;
using PoC.Shared.Events;
using PoC.Shared.Models;
using System.Text.Json;

namespace PoC.Costing.Functions;

public class PriceIngestionFunction
{
    private readonly ICostingRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceIngestionFunction"/> class.
    /// Default constructor for Lambda runtime.
    /// </summary>
    public PriceIngestionFunction()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        services.AddLogging(logging =>
        {
            logging.AddConfiguration(configuration.GetSection("Logging"));
            logging.AddConsole();
        });

        services.AddCostingInfrastructure(configuration);

        var serviceProvider = services.BuildServiceProvider();
        _repository = serviceProvider.GetRequiredService<ICostingRepository>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceIngestionFunction"/> class.
    /// Constructor for testing.
    /// </summary>
    /// <param name="repository">The costing repository.</param>
    public PriceIngestionFunction(ICostingRepository repository)
    {
        _repository = repository;
    }

#pragma warning disable VSTHRD200
    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
#pragma warning restore VSTHRD200
    {
        context.Logger.LogInformation($"[PriceIngestion] Processing {sqsEvent.Records.Count} SQS messages");

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                await ProcessSqsRecordAsync(record, context);
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"[PriceIngestion] Error processing record {record.MessageId}: {ex.Message}");
            }
        }
    }

    private async Task ProcessSqsRecordAsync(SQSEvent.SQSMessage record, ILambdaContext context)
    {
        using var doc = JsonDocument.Parse(record.Body);
        var root = doc.RootElement;

        // SNS wraps the message in "Message" property
        if (!root.TryGetProperty("Message", out var messageProperty))
        {
            context.Logger.LogWarning($"[PriceIngestion] Record {record.MessageId} has no 'Message' property");
            return;
        }

        var messageJson = messageProperty.GetString();
        if (string.IsNullOrEmpty(messageJson))
        {
             context.Logger.LogWarning($"[PriceIngestion] Record {record.MessageId} has empty message");
             return;
        }

        var priceEvent = JsonSerializer.Deserialize<PriceUpdatedEvent>(
            messageJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (priceEvent == null || string.IsNullOrEmpty(priceEvent.ComponentName))
        {
             context.Logger.LogWarning($"[PriceIngestion] Record {record.MessageId} has invalid price event");
             return;
        }

        context.Logger.LogInformation($"[PriceIngestion] Ingesting price for: {priceEvent.ComponentName}");

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

        context.Logger.LogInformation($"[PriceIngestion] Successfully ingested price for {priceEvent.ComponentName}");
    }
}
