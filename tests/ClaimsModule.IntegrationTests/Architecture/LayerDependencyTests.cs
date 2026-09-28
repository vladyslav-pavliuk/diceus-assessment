using NetArchTest.Rules;

namespace ClaimsModule.IntegrationTests.Architecture;

/// <summary>
/// The Clean Architecture dependency rule (brief §2.4): Domain ← Application ← (Infrastructure,
/// Persistence) ← API. Project references already forbid most violations; these tests also catch
/// use of framework types that would leak through transitive references.
/// </summary>
public sealed class LayerDependencyTests
{
    [Fact]
    public void CONV_14_Domain_depends_on_no_other_layer_or_framework()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain).ShouldNot().HaveDependencyOnAny(
                "ClaimsModule.Application",
                "ClaimsModule.Infrastructure",
                "ClaimsModule.Persistence",
                "ClaimsModule.API",
                "MediatR",
                "AutoMapper",
                "FluentValidation",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Hangfire",
                "Azure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void CONV_14_Application_does_not_depend_on_outer_layers_EF_Core_or_Azure()
    {
        var result = Types.InAssembly(SolutionAssemblies.Application).ShouldNot().HaveDependencyOnAny(
                "ClaimsModule.Infrastructure",
                "ClaimsModule.Persistence",
                "ClaimsModule.API",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Data.SqlClient",
                "Microsoft.AspNetCore",
                "Hangfire",
                "Azure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    /// <summary>The Hangfire jobs live here, so they can only reach data through Application commands (D-41).</summary>
    [Fact]
    public void CONV_14_Infrastructure_does_not_depend_on_Persistence_API_or_EF_Core()
    {
        var result = Types.InAssembly(SolutionAssemblies.Infrastructure).ShouldNot().HaveDependencyOnAny(
                "ClaimsModule.Persistence",
                "ClaimsModule.API",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void CONV_14_Persistence_does_not_depend_on_Infrastructure_API_or_Hangfire()
    {
        var result = Types.InAssembly(SolutionAssemblies.Persistence).ShouldNot().HaveDependencyOnAny(
                "ClaimsModule.Infrastructure",
                "ClaimsModule.API",
                "Microsoft.AspNetCore",
                "Hangfire")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void CONV_14_Controllers_stay_thin_and_never_touch_persistence()
    {
        var result = Types.InAssembly(SolutionAssemblies.Api)
            .That().HaveNameEndingWith("Controller")
            .ShouldNot().HaveDependencyOnAny("ClaimsModule.Persistence", "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void CONV_09_Domain_uses_no_data_annotations()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot().HaveDependencyOn("System.ComponentModel.DataAnnotations")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    internal static string Describe(TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
