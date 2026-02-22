using Amazon.DynamoDBv2.DataModel;
using System.Text.Json.Serialization;

namespace PoC.Materials.Infrastructure.Persistence;

[DynamoDBTable("materials-table")]
public class MaterialEntity
{
    [DynamoDBHashKey("material_id")]
    [DynamoDBGlobalSecondaryIndexRangeKey("IX_Materials_By_Type")]
    [JsonPropertyName("material_id")]
    public string MaterialId { get; set; } = string.Empty;

    [DynamoDBGlobalSecondaryIndexHashKey("IX_Materials_By_Type")]
    [DynamoDBProperty("record_type")]
    [JsonPropertyName("record_type")]
    public string RecordType { get; set; } = "MATERIAL";

    [DynamoDBProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDBProperty("density")]
    [JsonPropertyName("density")]
    public DensityEntity? Density { get; set; }

    [DynamoDBProperty("formulation")]
    [JsonPropertyName("formulation")]
    public List<FormulationComponentEntity> Formulation { get; set; } = new();

    [DynamoDBProperty("properties")]
    [JsonPropertyName("properties")]
    public Dictionary<string, string> Properties { get; set; } = new();

    [DynamoDBVersion]
    [JsonPropertyName("Version")] // DynamoDBVersion usually maps to the attribute name defined or default. The attribute name isn't specified here, so it defaults to "Version".
    public int? Version { get; set; }
}

public class DensityEntity
{
    [DynamoDBProperty("value")]
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [DynamoDBProperty("unit")]
    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;
}

public class FormulationComponentEntity
{
    [DynamoDBProperty("component")]
    [JsonPropertyName("component")]
    public string Component { get; set; } = string.Empty;

    [DynamoDBProperty("percentage")]
    [JsonPropertyName("percentage")]
    public double Percentage { get; set; }

    [DynamoDBProperty("type")]
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}
