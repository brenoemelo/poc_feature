using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using PoC.Costing.Domain.Services;
using PoC.Costing.Infrastructure;
using PoC.Shared.Common;
using PoC.Shared.Models;
using PoC.Observability.Tests;
using Xunit;

namespace PoC.Costing.Tests;

public class CostingTelemetryTests : IClassFixture<OtelTestFixture>
{
    private readonly OtelTestFixture _fixture;

    public CostingTelemetryTests(OtelTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void BusinessMetrics_ShouldEmitMetric_WhenRecorded()
    {
        // Arrange
        // 1. Setup IMeterFactory to allow BusinessMetrics to create the Meter
        var services = new ServiceCollection();
        services.AddMetrics(); // Registers IMeterFactory default implementation
        services.AddSingleton<BusinessMetrics>();
        var sp = services.BuildServiceProvider();

        var businessMetrics = sp.GetRequiredService<BusinessMetrics>();
        
        // Note: CostCalculator is pure domain and doesn't emit metrics directly.
        // Metrics are emitted by the Application Layer (Endpoints/Handlers).
        // This test verifies that BusinessMetrics correctly records and emits values when called.

        // Act
        businessMetrics.RecordCalculation(150.00);

        // Force flush to ensure metrics are exported to our fixture
        _fixture.ForceFlush();

        // Assert
        var metric = _fixture.ExportedMetrics
            .FirstOrDefault(m => m.Name == "business.costing.calculation.count");

        metric.Should().NotBeNull("Metric 'business.costing.calculation.count' should be emitted");
        
        var sum = 0L;
        foreach (var point in metric!.GetMetricPoints())
        {
             sum += point.GetSumLong();
        }
        sum.Should().BeGreaterThan(0);
        
        // Also verify the histogram value
        var histogram = _fixture.ExportedMetrics
            .FirstOrDefault(m => m.Name == "business.costing.value");
        histogram.Should().NotBeNull();
    }
}
