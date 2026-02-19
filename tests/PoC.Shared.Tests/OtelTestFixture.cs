using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Logging;

namespace PoC.Shared.Tests;

public class OtelTestFixture : IDisposable
{
    private readonly TracerProvider _tracerProvider;
    private readonly MeterProvider _meterProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<Activity> _exportedActivities = new();
    private readonly List<Metric> _exportedMetrics = new();
    private readonly List<LogRecord> _exportedLogs = new();

    public string ServiceName { get; } = $"TestService-{Guid.NewGuid()}";
    
    // Expose the exported data for assertions
    public IReadOnlyList<Activity> ExportedActivities => _exportedActivities;
    public IReadOnlyList<Metric> ExportedMetrics => _exportedMetrics;
    public IReadOnlyList<LogRecord> ExportedLogs => _exportedLogs;
    public ILoggerFactory LoggerFactory => _loggerFactory;

    public OtelTestFixture()
    {
        // Configure Tracing
        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ServiceName) // Source for the test service itself
            .AddSource("PoC.*")     // Source for any PoC components
            .ConfigureResource(r => r.AddService(ServiceName))
            .AddInMemoryExporter(_exportedActivities)
            .Build();

        // Configure Metrics
        _meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(ServiceName)
            .AddMeter("PoC.*")
            .ConfigureResource(r => r.AddService(ServiceName))
            .AddInMemoryExporter(_exportedMetrics)
            .Build();

        // Configure Logging
        _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ServiceName));
                options.AddInMemoryExporter(_exportedLogs);
            });
        });
    }

    /// <summary>
    /// Forces a flush of the providers to ensure all data is exported to the lists.
    /// </summary>
    public void ForceFlush()
    {
        _tracerProvider.ForceFlush();
        _meterProvider.ForceFlush();
        // Log flushing is implicit or handled by dispose/processor, but OpenTelemetryLoggerOptions doesn't expose ForceFlush directly on the factory easily
        // typically implicit flush happens on dispose, but for tests we might rely on the processor being simple.
        // The InMemoryExporter for logs usually writes synchronously.
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tracerProvider?.Dispose();
            _meterProvider?.Dispose();
            _loggerFactory?.Dispose();
        }
    }
}
