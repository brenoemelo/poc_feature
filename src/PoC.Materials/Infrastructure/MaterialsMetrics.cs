using System.Diagnostics.Metrics;

namespace PoC.Materials.Infrastructure;

public class MaterialsMetrics
{
    public const string MeterName = "PoC.Materials";

    private readonly Counter<long> _ingestionCounter;
    private readonly Histogram<double> _processingDuration;

    public MaterialsMetrics()
    {
        var meter = new Meter(MeterName, "1.0.0");
        _ingestionCounter = meter.CreateCounter<long>("business.materials.ingestion.count", "items", "Count of ingested materials");
        _processingDuration = meter.CreateHistogram<double>("business.materials.processing.duration", "ms", "Processing duration of material ingestion");
    }

    public void RecordIngestion(string status)
    {
        _ingestionCounter.Add(1, new KeyValuePair<string, object?>("status", status));
    }

    public void RecordProcessingDuration(double durationMs)
    {
        _processingDuration.Record(durationMs);
    }
}
