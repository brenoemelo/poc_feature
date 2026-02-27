namespace PoC.Populator.Configuration;

public sealed class PopulatorOptions
{
    public const string SectionName = "Populator";

    public string MaterialsTableName { get; set; } = "materials-table";
    public string PricesTableName { get; set; } = "prices-table";
}
