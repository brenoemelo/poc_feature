using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace PoC.Shared.Tests;

public class LoggingTelemetryTests : IClassFixture<OtelTestFixture>
{
    private readonly OtelTestFixture _fixture;

    public LoggingTelemetryTests(OtelTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Log_ShouldBeExported_WithTraceContext()
    {
        // Arrange
        var logger = _fixture.LoggerFactory.CreateLogger<LoggingTelemetryTests>();
        var activitySource = new ActivitySource("PoC.Tests"); // Matches "PoC.*" subscription in fixture

        // Act
        string traceId;
        string spanId;
        
        using (var activity = activitySource.StartActivity("TestLogActivity"))
        {
            Assert.NotNull(activity);
            traceId = activity.TraceId.ToString();
            spanId = activity.SpanId.ToString();
            
            logger.LogInformation("Test log message inside activity");
        }

        // Force flush (though logs are usually immediate in InMemory, this is safe)
        _fixture.ForceFlush();

        // Assert
        var log = _fixture.ExportedLogs
            .FirstOrDefault(l => l.Body == "TestLogActivity" || l.Body == "Test log message inside activity");

        // Note: OpenTelemetry LogRecord.Body is the formatted message or the state. 
        // Let's find by checking if any log contains our text.
        
        // Note: OpenTelemetry LogRecord.Body is the formatted message or the state. 
        // In simple logs, Body often contains the string message.
        
        var matchingLog = _fixture.ExportedLogs.FirstOrDefault(l => l.Body == "Test log message inside activity" || l.FormattedMessage == "Test log message inside activity");

        matchingLog.Should().NotBeNull("Log should be exported");
        matchingLog!.TraceId.ToString().Should().Be(traceId, "Log should have correct TraceId");
        matchingLog.SpanId.ToString().Should().Be(spanId, "Log should have correct SpanId");
        matchingLog.CategoryName.Should().Be(typeof(LoggingTelemetryTests).FullName);
    }
}
