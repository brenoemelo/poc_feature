using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;

namespace PoC.Observability.Diagnostics;

public class InstrumentationSource : IHostedService, IDisposable
{
    private readonly Meter _meter;
    private readonly StartupTimer _startupTimer;

    public InstrumentationSource(StartupTimer startupTimer)
    {
        _startupTimer = startupTimer;
        _meter = new Meter("app.startup", "1.0.0");

        _meter.CreateObservableGauge("app.startup_duration_ms", () => _startupTimer.Elapsed.TotalMilliseconds);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Metric is created in constructor, nothing to do here explicitly
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
