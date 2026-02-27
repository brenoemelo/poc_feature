using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PoC.Observability.Diagnostics;

public class InstrumentationSource : IHostedService, IDisposable
{
    private readonly Meter _meter;
    private readonly StartupTimer _startupTimer;
    private readonly ILogger<InstrumentationSource> _logger;

    public InstrumentationSource(StartupTimer startupTimer, ILogger<InstrumentationSource> logger)
    {
        _startupTimer = startupTimer;
        _logger = logger;
        _meter = new Meter("app.startup", "1.0.0");

        _meter.CreateObservableGauge("app.startup_duration_ms", () => 
        {
            var elapsed = _startupTimer.ElapsedMilliseconds;
            // Only log once when it stops and we observe it
            return elapsed;
        }, unit: "ms", description: "The time it took for the application to start");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _startupTimer.Stop();
        _logger.LogInformation("Application started in {ElapsedMilliseconds}ms", _startupTimer.ElapsedMilliseconds);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _meter.Dispose();
        GC.SuppressFinalize(this);
    }
}