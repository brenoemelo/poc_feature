namespace PoC.Shared.Models;

public class PopulationRequest
{
    public string Target { get; set; } = "materials";
    public int Count { get; set; }
}

public class PopulationJob
{
    public string Target { get; set; } = string.Empty;
    public int BatchSize { get; set; }
}
