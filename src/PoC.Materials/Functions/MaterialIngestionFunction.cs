using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using PoC.Materials.Domain.Interfaces;
using PoC.Materials.Infrastructure;
using PoC.Shared.Events;
using System.Text.Json;

namespace PoC.Materials.Functions;

public class MaterialIngestionFunction
{
    private readonly IMaterialRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialIngestionFunction"/> class.
    /// Default constructor for Lambda runtime.
    /// Initializes dependency injection container and resolves dependencies.
    /// </summary>
    public MaterialIngestionFunction()
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

        services.AddMaterialsInfrastructure(configuration);

        var serviceProvider = services.BuildServiceProvider();
        _repository = serviceProvider.GetRequiredService<IMaterialRepository>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialIngestionFunction"/> class.
    /// Constructor for testing or manual dependency injection.
    /// </summary>
    /// <param name="repository">The material repository.</param>
    public MaterialIngestionFunction(IMaterialRepository repository)
    {
        _repository = repository;
    }

#pragma warning disable VSTHRD200
    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
#pragma warning restore VSTHRD200
    {
        context.Logger.LogInformation($"[MaterialIngestion] Processing {sqsEvent.Records.Count} SQS messages");

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                await ProcessSqsRecordAsync(record, context);
            }
            catch (Exception ex)
            {
                context.Logger.LogError(
                    $"[MaterialIngestion] Error processing record {record.MessageId}: {ex.Message}");
            }
        }

        context.Logger.LogInformation("[MaterialIngestion] Batch processing complete");
    }

    private async Task ProcessSqsRecordAsync(SQSEvent.SQSMessage record, ILambdaContext context)
    {
        context.Logger.LogInformation($"[MaterialIngestion] Raw Body Length: {record.Body?.Length ?? 0}");
        
        if (string.IsNullOrEmpty(record.Body))
        {
            context.Logger.LogWarning($"[MaterialIngestion] Record {record.MessageId} has empty body");
            return;
        }

        var snippet = record.Body.Length > 200 ? record.Body.Substring(0, 200) : record.Body;
        context.Logger.LogInformation($"[MaterialIngestion] Raw Body Snippet: {snippet}");

        using var doc = JsonDocument.Parse(record.Body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("Message", out var messageProperty))
        {
            context.Logger.LogWarning(
                $"[MaterialIngestion] Record {record.MessageId} has no 'Message' property");
            return;
        }

        var messageJson = messageProperty.GetString();
        if (string.IsNullOrEmpty(messageJson))
        {
            context.Logger.LogWarning(
                $"[MaterialIngestion] Record {record.MessageId} has empty message");
            return;
        }

        var materialEvent = JsonSerializer.Deserialize<MaterialCreatedEvent>(
            messageJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (materialEvent?.Material is null)
        {
            context.Logger.LogWarning(
                $"[MaterialIngestion] Record {record.MessageId} has null material");
            return;
        }

        context.Logger.LogInformation(
            $"[MaterialIngestion] Ingesting material: {materialEvent.Material.Name} ({materialEvent.Material.MaterialId})");

        var result = await _repository.SaveAsync(materialEvent.Material);

        if (result.IsFailure)
        {
             // Idempotency check: If the material already exists (conditional check failed), we consider it a success.
             if (result.Error.Code == "DynamoDb.Error" && result.Error.Description.Contains("conditional request failed", StringComparison.OrdinalIgnoreCase))
             {
                 context.Logger.LogWarning($"[MaterialIngestion] Material {materialEvent.Material.MaterialId} ingestion failed due to Optimistic Locking (Version mismatch or Item already exists). Skipping (idempotent).");
                 return;
             }

             throw new InvalidOperationException($"Failed to ingest material: {result.Error.Code} - {result.Error.Description}");
        }

        context.Logger.LogInformation(
            $"[MaterialIngestion] Successfully ingested material {materialEvent.Material.MaterialId}");
    }
}
