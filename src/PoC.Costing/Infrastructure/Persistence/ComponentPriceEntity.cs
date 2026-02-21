using Amazon.DynamoDBv2.DataModel;

namespace PoC.Costing.Infrastructure.Persistence;

public class ComponentPriceEntity
{
    [DynamoDBHashKey]
    public string ComponentName { get; set; } = string.Empty;

    [DynamoDBProperty]
    public decimal UnitPrice { get; set; }

    [DynamoDBProperty]
    public string Unit { get; set; } = string.Empty;

    [DynamoDBProperty]
    public string Currency { get; set; } = string.Empty;

    [DynamoDBProperty]
    public DateTime UpdatedAt { get; set; }

    [DynamoDBVersion]
    public int? Version { get; set; }
}
