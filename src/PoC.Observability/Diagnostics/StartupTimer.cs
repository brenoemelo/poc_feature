using System.Diagnostics;

namespace PoC.Observability.Diagnostics;

public class StartupTimer
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan? _duration;

    public void Stop()
    {
        if (_duration == null)
        {
            _stopwatch.Stop();
            _duration = _stopwatch.Elapsed;
        }
    }

    public double ElapsedMilliseconds => _duration?.TotalMilliseconds ?? _stopwatch.Elapsed.TotalMilliseconds;
}