using System.Diagnostics.Metrics;

namespace PoC.Shared.Infrastructure.Diagnostics;

/// <summary>
/// Helper class to emit startup duration metrics.
/// </summary>
internal sealed class InstrumentationSource : IDisposable
{
    private readonly Meter _meter;

    public InstrumentationSource(StartupTimer timer)
    {
        _meter = new Meter("app.startup");
        _meter.CreateObservableGauge(
            "app.startup_duration_ms", 
            () => timer.ElapsedMilliseconds, 
            unit: "ms", 
            description: "Application startup duration in milliseconds.");
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
