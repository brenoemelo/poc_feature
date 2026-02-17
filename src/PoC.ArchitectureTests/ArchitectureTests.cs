using NetArchTest.Rules;
using System.Reflection;
using Xunit; // Assuming xUnit

namespace PoC.ArchitectureTests;

public class ArchitectureTests
{
    // 2. Layer Definitions
    private const string DomainNamespace = ".Domain";
    private const string ApplicationNamespace = ".Application";
    private const string InfrastructureNamespace = ".Infrastructure";
    private const string ApiNamespace = ".API";
    private const string SharedKernelNamespace = "PoC.Shared";

    // 1. Dynamic Assembly Loading
    // Instead of hardcoding modules, we inspect the running application to find all project assemblies.
    private static readonly List<Assembly> ProjectAssemblies = AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => a.FullName!.StartsWith("PoC.") && !a.FullName.Contains("Tests"))
        .ToList();

    // ==========================================
    // 1. GLOBAL LAYERING RULES (Clean Architecture)
    // ==========================================
    [Fact]
    public void Domain_Layer_Should_Not_Depend_On_Any_Outer_Layer()
    {
        // Rule: Domain is pure. It creates the rules. It knows nothing about databases or APIs.
        var result = Types.InAssemblies(ProjectAssemblies)
            .That()
            .ResideInNamespaceEndingWith(DomainNamespace)
            .ShouldNot()
            .HaveDependencyOn(ApplicationNamespace)
            .And()
            .HaveDependencyOn(InfrastructureNamespace)
            .And()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Violation: Domain layer must strictly not depend on Application, Infrastructure, or API layers.");
    }

    [Fact]
    public void Application_Layer_Should_Not_Depend_On_Infrastructure_Directly()
    {
        // Rule: Application orchestrates logic. It uses Interfaces defined in Core, implemented in Infra.
        // It should NOT instantiate concrete Infra classes directly.
        var result = Types.InAssemblies(ProjectAssemblies)
            .That()
            .ResideInNamespaceEndingWith(ApplicationNamespace)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Violation: Application layer must not depend directly on Infrastructure implementations.");
    }

    [Fact]
    public void Shared_Kernel_Should_Not_Depend_On_Feature_Domains()
    {
        // Rule: Shared Kernel (Infra logic) cannot know about specific Business Domains (Materials, Costing).
        // This prevents circular dependencies and "God Objects".
        var forbiddenDependencies = new[] { "PoC.Materials", "PoC.Costing", "PoC.Populator" };

        var result = Types.InAssemblies(ProjectAssemblies)
            .That()
            .ResideInNamespace(SharedKernelNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenDependencies)
            .GetResult();

        Assert.True(result.IsSuccessful, "Violation: Shared Kernel must remain agnostic of specific business domains.");
    }

    // ==========================================
    // 2. CODING STANDARDS & BEST PRACTICES
    // ==========================================
    [Fact]
    public void DTOs_And_Messages_Should_Be_Records_Or_Sealed()
    {
        // Rule: Data Transfer Objects and Messages should be immutable (Records) or explicitly Sealed for performance.
        var result = Types.InAssemblies(ProjectAssemblies)
            .That()
            .ResideInNamespaceEndingWith(".Models") // Or .DTOs, .Contracts
            .And()
            .AreClasses()
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful, "Violation: DTOs/Models must be 'sealed' classes or 'records' (which are sealed by default).");
    }

    [Fact]
    public void Repositories_Should_Be_Internal_And_In_Infrastructure()
    {
        // Rule: Repositories are implementation details. They should not be public outside the assembly (usually).
        // They must reside in Infrastructure.
        var result = Types.InAssemblies(ProjectAssemblies)
            .That()
            .HaveNameEndingWith("Repository")
            .Should()
            .ResideInNamespaceEndingWith(InfrastructureNamespace)
            .And()
            .NotBePublic() // Or strictly Internal
            .GetResult();

        Assert.True(result.IsSuccessful, "Violation: Repositories must be in Infrastructure layer and should not be public (use interfaces).");
    }

    // ==========================================
    // 3. NAMING CONVENTIONS (English Only)
    // ==========================================
    [Fact]
    public void Interfaces_Should_Start_With_I()
    {
        var result = Types.InAssemblies(ProjectAssemblies)
            .That()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        Assert.True(result.IsSuccessful, "Violation: Interfaces must start with 'I'.");
    }
}
