# Implementing Custom Business Metrics in .NET 8

To feed the **Business KPIs Dashboard**, you need to emit custom metrics from your Domain logic. We use `System.Diagnostics.Metrics` (part of the BCL), which OpenTelemetry automatically instantiates.

## 1. Define a Metrics Service

Create a wrapper to keep your metric definitions organized.

```csharp
using System.Diagnostics.Metrics;

public class BusinessMetrics
{
    public const string MeterName = "PoC.Business";
    private readonly Meter _meter;

    // Counters
    private readonly Counter<long> _calculationCount;
    private readonly Counter<long> _materialIngestionCount;

    // Histograms (for Values/Duration)
    private readonly Histogram<double> _costValue;
    private readonly Histogram<double> _processingDuration;

    public BusinessMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _calculationCount = _meter.CreateCounter<long>(
            "business.costing.calculation.count",
            description: "Number of cost calculations performed");

        _costValue = _meter.CreateHistogram<double>(
            "business.costing.value",
            unit: "USD",
            description: "The calculated cost value");

        _materialIngestionCount = _meter.CreateCounter<long>(
            "business.materials.ingestion.count",
            description: "Number of materials ingested");

        _processingDuration = _meter.CreateHistogram<double>(
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
```

## 2. Register in DI (Program.cs)

```csharp
builder.Services.AddSingleton<BusinessMetrics>();
```

## 3. Enable the Meter in OpenTelemetry

You MUST tell OpenTelemetry to listen to this new Meter (`PoC.Business`).

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        // ... other instrumentations
        .AddMeter("PoC.Business") // <--- IMPORTANT: Add your custom meter name here
        .AddOtlpExporter());
```

## 4. Usage in Domain Service

```csharp
public class CostingService
{
    private readonly BusinessMetrics _metrics;

    public CostingService(BusinessMetrics metrics)
    {
        _metrics = metrics;
    }

    public double CalculateCost(Material material)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // ... business logic ...
        double cost = 150.00;

        stopwatch.Stop();
        
        // Emit Metrics
        _metrics.RecordCalculation(cost);
        
        return cost;
    }
}
```
