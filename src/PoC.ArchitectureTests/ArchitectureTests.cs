using NetArchTest.Rules;
using Xunit;
using PoC.Shared;
using PoC.Lambda;
using PoC.Populator;
using PoC.Costing;

namespace PoC.ArchitectureTests;

public class ArchitectureTests
{
    // Mapeamento dos Namespaces para o projeto PoC
    private const string DomainNamespace = "PoC.Shared";
    private const string LambdaNamespace = "PoC.Lambda";
    private const string PopulatorNamespace = "PoC.Populator";
    private const string CostingNamespace = "PoC.Costing";

    [Fact]
    public void Domain_Should_Not_Have_Dependency_On_Other_Layers()
    {
        // O Domínio (Shared) deve ser puro. Não pode conhecer as implementações das Lambdas.
        var result = Types.InAssembly(typeof(PoC.Shared.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(LambdaNamespace)
            .And()
            .HaveDependencyOn(PopulatorNamespace)
            .And()
            .HaveDependencyOn(CostingNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain (PoC.Shared) should not depend on other layers.");
    }

    [Fact]
    public void Lambda_Functions_Should_Have_Function_Or_Handler_Suffix()
    {
        // Adaptado para o contexto de Lambda
        var result = Types.InAssembly(typeof(PoC.Lambda.AssemblyMarker).Assembly)
            .That()
            .HaveNameEndingWith("Function")
            .Or()
            .HaveNameEndingWith("Handler")
            .Should()
            .BePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, "Lambda entry points should follow naming conventions.");
    }

    [Fact]
    public void Domain_Entities_Should_Be_Sealed_Or_Abstract()
    {
        // Regra de design: Entidades devem ser seladas para performance (DynamoDB context)
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
    public void Populator_Should_Not_Depend_On_Lambda()
    {
        // Populator e Lambda devem ser independentes
        var result = Types.InAssembly(typeof(PoC.Populator.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(LambdaNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "PoC.Populator should not depend on PoC.Lambda.");
    }

    [Fact]
    public void Costing_Should_Not_Depend_On_Lambda_Or_Populator()
    {
        // Costing deve ser independente de Lambda e Populator
        var result = Types.InAssembly(typeof(PoC.Costing.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(LambdaNamespace)
            .And()
            .HaveDependencyOn(PopulatorNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "PoC.Costing should not depend on PoC.Lambda or PoC.Populator.");
    }

    [Fact]
    public void Costing_Functions_Should_Follow_Naming_Convention()
    {
        // Funções do Costing devem terminar com "Function"
        var result = Types.InAssembly(typeof(PoC.Costing.AssemblyMarker).Assembly)
            .That()
            .HaveNameEndingWith("Function")
            .Should()
            .BePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, "Costing functions should follow naming conventions.");
    }
}
