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
    public void Calculate_ShouldEmitBusinessCostCalculatedMetric()
    {
        // Arrange
        // 1. Setup IMeterFactory to allow BusinessMetrics to create the Meter
        var services = new ServiceCollection();
        services.AddMetrics(); // Registers IMeterFactory default implementation
        services.AddSingleton<BusinessMetrics>();
        var sp = services.BuildServiceProvider();

        var businessMetrics = sp.GetRequiredService<BusinessMetrics>();
        var calculator = new CostCalculator(businessMetrics);

        var formulation = new List<FormulationInput>
        {
            new("Material A", 50),
            new("Material B", 50)
        };
        var prices = new Dictionary<string, (decimal, string)>
        {
            { "Material A", (10m, "USD") },
            { "Material B", (20m, "USD") }
        };

        // Act
        var result = calculator.Calculate("test-material", formulation, 20, prices);

        // Force flush to ensure metrics are exported to our fixture
        _fixture.ForceFlush();

        // Assert
        result.IsSuccess.Should().BeTrue();

        var metric = _fixture.ExportedMetrics
            .FirstOrDefault(m => m.Name == "business.costing.calculation.count");

        metric.Should().NotBeNull("Metric 'business.costing.calculation.count' should be emitted");
        
        // Setup assertions for the metric value
        // Note: In a real scenario with shared fixture, this count might be > 1.
        // Usually we check if it increased, or use a fresh fixture per test.
        // For this PoC, checking existence and value > 0 is a good start.
        var sum = 0L;
        foreach (var point in metric!.GetMetricPoints())
        {
             sum += point.GetSumLong();
        }
        sum.Should().BeGreaterThan(0);
    }
}
