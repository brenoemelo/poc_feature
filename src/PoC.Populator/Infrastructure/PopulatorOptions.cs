namespace PoC.Populator.Infrastructure;

public sealed class PopulatorOptions
{
    public const string SectionName = "Populator";

    public string QueueUrl { get; set; } = string.Empty;
}
