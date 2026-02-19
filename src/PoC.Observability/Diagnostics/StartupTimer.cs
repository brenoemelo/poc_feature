using System.Diagnostics;

namespace PoC.Observability.Diagnostics;

public class StartupTimer
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public TimeSpan Elapsed => _stopwatch.Elapsed;
}
