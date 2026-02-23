namespace PoC.Populator.Configuration;

public class AwsOptions
{
    public const string SectionName = "AWS";

    public string ServiceUrl { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public string SqsQueueUrl { get; set; } = string.Empty;
    public string SnsTopicArn { get; set; } = string.Empty;
}
