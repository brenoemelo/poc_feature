namespace PoC.Costing.Configuration;

public class AwsOptions
{
    public const string SectionName = "AWS";

    public string ServiceUrl { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
}
