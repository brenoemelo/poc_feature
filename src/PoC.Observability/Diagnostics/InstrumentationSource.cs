using System.Diagnostics.Metrics;

namespace PoC.Observability.Diagnostics;

public class InstrumentationSource : IDisposable
{
    private readonly Meter _meter;
    private readonly StartupTimer _startupTimer;

    public InstrumentationSource(StartupTimer startupTimer)
    {
        _startupTimer = startupTimer;
        _meter = new Meter("app.startup", "1.0.0");

        _meter.CreateObservableGauge("app.startup_duration_ms", () => _startupTimer.Elapsed.TotalMilliseconds);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
