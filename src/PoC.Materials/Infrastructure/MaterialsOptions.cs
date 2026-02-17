namespace PoC.Materials.Infrastructure;

public sealed class MaterialsOptions
{
    public const string SectionName = "Materials";

    public string TableName { get; set; } = "materials-table";
}
