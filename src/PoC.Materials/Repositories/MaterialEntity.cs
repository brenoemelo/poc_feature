using Amazon.DynamoDBv2.DataModel;

namespace PoC.Materials.Repositories;

[DynamoDBTable("materials-table")]
public class MaterialEntity
{
    [DynamoDBHashKey("material_id")]
    public string MaterialId { get; set; } = string.Empty;

    [DynamoDBProperty("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDBProperty("density")]
    public DensityEntity? Density { get; set; }

    [DynamoDBProperty("formulation")]
    public List<FormulationComponentEntity> Formulation { get; set; } = new();

    [DynamoDBProperty("properties")]
    public Dictionary<string, string> Properties { get; set; } = new();
}

public class DensityEntity
{
    [DynamoDBProperty("value")]
    public double Value { get; set; }

    [DynamoDBProperty("unit")]
    public string Unit { get; set; } = string.Empty;
}

public class FormulationComponentEntity
{
    [DynamoDBProperty("component")]
    public string Component { get; set; } = string.Empty;

    [DynamoDBProperty("percentage")]
    public double Percentage { get; set; }

    [DynamoDBProperty("type")]
    public string Type { get; set; } = string.Empty;
}
