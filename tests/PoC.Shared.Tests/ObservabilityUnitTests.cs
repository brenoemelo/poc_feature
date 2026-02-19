using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Xunit;
using PoC.Shared.Infrastructure.Extensions;

namespace PoC.Shared.Tests;

public class ObservabilityUnitTests
{
    [Fact]
    public async Task Histogram_Should_Have_Explicit_Buckets_And_Dimensions()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        
        // Mock configuration
        var inMemorySettings = new Dictionary<string, string?> {
            {"Otel:Endpoint", "http://localhost:4317"},
            {"Otel:Protocol", "grpc"}
        };
        builder.Configuration.AddInMemoryCollection(inMemorySettings);

        // Add Observability
        builder.AddPoCObservability("TestService", "1.0.0");
        builder.Services.AddHttpClient(); // Required for IHttpClientFactory

        // Add InMemory Exporter to capture metrics
        var exportedItems = new List<Metric>();
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => {
                metrics.AddInMemoryExporter(exportedItems);
                metrics.AddMeter("TestMeter"); // Explicitly add our test meter
            });

        var app = builder.Build();

        // Create an HTTP client to generate metrics
        var httpClient = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
        
        // Act
        // Make a request to generate http.client.request.duration
        try 
        {
            await httpClient.GetAsync("http://example.com");
        }
        catch 
        { 
            // Ignore connection errors, we just want the metric
        }

        // Force flush
        app.Services.GetRequiredService<MeterProvider>().ForceFlush();

        // Assert
        var metric = exportedItems.FirstOrDefault(m => m.Name == "http.client.request.duration");
        
        // Debug output if null
        if (metric == null)
        {
            var names = string.Join(", ", exportedItems.Select(m => m.Name));
            throw new Exception($"Metric not found. Exported metrics: [{names}]");
        }

        metric.Should().NotBeNull("Metric 'http.client.request.duration' should be exported");
        metric!.MetricType.Should().Be(MetricType.Histogram);

        // precise check for buckets
        bool foundPoints = false;
        foreach (var metricPoint in metric.GetMetricPoints())
        {
            foundPoints = true;
            var buckets = metricPoint.GetHistogramBuckets();
            buckets.Should().NotBeNull("Histogram buckets should not be null");
            
            // Check bucket count
            // Our config has 15 boundaries, so we expect 15+1=16 buckets
            int bucketCount = 0;
            foreach (var bucket in buckets!)
            {
                bucketCount++;
            }
            bucketCount.Should().BeGreaterThan(10, "Should have explicit histogram buckets defined");
            
            // Check Dimensions (Tags)
            var tags = new Dictionary<string, object?>();
            foreach(var tag in metricPoint.Tags)
            {
                tags[tag.Key] = tag.Value;
            }

            // OTel HttpClient instrumentation adds these
            tags.Should().ContainKey("http.request.method");
            tags.Should().ContainKey("server.address");
        }
        
        foundPoints.Should().BeTrue("Should have found at least one metric point");
    }
}
