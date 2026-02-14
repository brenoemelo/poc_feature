using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using PoC.Materials.Repositories;
using PoC.Shared.Events;

namespace PoC.Materials.Functions;

public class MaterialIngestionFunction(IMaterialRepository repository)
{
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

        await repository.SaveAsync(materialEvent.Material);

        context.Logger.LogInformation(
            $"[MaterialIngestion] Successfully ingested material {materialEvent.Material.MaterialId}");
    }
}
