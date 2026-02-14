using NetArchTest.Rules;
using PoC.Costing;
using PoC.Materials;
using PoC.Populator;

namespace PoC.ArchitectureTests;

public class ArchitectureTests
{
    private const string MaterialsNamespace = "PoC.Materials";
    private const string PopulatorNamespace = "PoC.Populator";
    private const string CostingNamespace = "PoC.Costing";

    [Fact]
    public void Domain_Should_Not_Have_Dependency_On_Other_Layers()
    {
        var result = Types.InAssembly(typeof(PoC.Shared.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(MaterialsNamespace)
            .And()
            .HaveDependencyOn(PopulatorNamespace)
            .And()
            .HaveDependencyOn(CostingNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain (PoC.Shared) should not depend on other layers.");
    }

    [Fact]
    public void Materials_Functions_Should_Follow_Naming_Convention()
    {
        var result = Types.InAssembly(typeof(PoC.Materials.AssemblyMarker).Assembly)
            .That()
            .HaveNameEndingWith("Function")
            .Should()
            .BePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, "Materials functions should follow naming conventions.");
    }

    [Fact]
    public void Populator_Functions_Should_Follow_Naming_Convention()
    {
        var result = Types.InAssembly(typeof(PoC.Populator.AssemblyMarker).Assembly)
            .That()
            .HaveNameEndingWith("Function")
            .Should()
            .BePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, "Populator functions should follow naming conventions.");
    }

    [Fact]
    public void Domain_Entities_Should_Be_Sealed_Or_Abstract()
    {
        var result = Types.InAssembly(typeof(PoC.Shared.AssemblyMarker).Assembly)
            .That()
            .Inherit(typeof(PoC.Shared.Common.BaseEntity))
            .Should()
            .BeSealed()
            .Or()
            .BeAbstract()
            .GetResult();

        Assert.True(result.IsSuccessful, "Entities in Shared should be sealed or abstract.");
    }

    [Fact]
    public void Costing_Should_Not_Depend_On_Populator()
    {
        var result = Types.InAssembly(typeof(PoC.Costing.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(PopulatorNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "PoC.Costing should not depend on PoC.Populator.");
    }

    [Fact]
    public void Costing_Functions_Should_Follow_Naming_Convention()
    {
        var result = Types.InAssembly(typeof(PoC.Costing.AssemblyMarker).Assembly)
            .That()
            .HaveNameEndingWith("Function")
            .Should()
            .BePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, "Costing functions should follow naming conventions.");
    }
}
