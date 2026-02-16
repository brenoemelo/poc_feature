using System.Diagnostics;

namespace PoC.Shared.Infrastructure.Diagnostics;

/// <summary>
/// Captures the initial timestamp of the application to measure startup duration.
/// </summary>
public sealed class StartupTimer
{
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();

    /// <summary>
    /// Gets the elapsed milliseconds since the timer was created.
    /// </summary>
    public double ElapsedMilliseconds => Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
}
