using System.Diagnostics.Metrics;

namespace PoC.Costing.Infrastructure;

public class BusinessMetrics
{
    public const string MeterName = "PoC.Business";
    
    // Counters
    private readonly Counter<long> _calculationCount;
    private readonly Counter<long> _materialIngestionCount;

    // Histograms (for Values/Duration)
    private readonly Histogram<double> _costValue;
    private readonly Histogram<double> _processingDuration;

    public BusinessMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _calculationCount = meter.CreateCounter<long>(
            "business.costing.calculation.count",
            description: "Number of cost calculations performed");

        _costValue = meter.CreateHistogram<double>(
            "business.costing.value",
            unit: "USD",
            description: "The calculated cost value");

        _materialIngestionCount = meter.CreateCounter<long>(
            "business.materials.ingestion.count",
            description: "Number of materials ingested");

        _processingDuration = meter.CreateHistogram<double>(
            "business.materials.processing.duration",
            unit: "ms",
            description: "Time taken to process business logic");
    }

    public void RecordCalculation(double cost)
    {
        _calculationCount.Add(1);
        _costValue.Record(cost);
    }

    public void RecordIngestion(bool success, double durationMs)
    {
        var status = success ? "success" : "failure";
        _materialIngestionCount.Add(1, new KeyValuePair<string, object?>("status", status));
        _processingDuration.Record(durationMs);
    }
}
