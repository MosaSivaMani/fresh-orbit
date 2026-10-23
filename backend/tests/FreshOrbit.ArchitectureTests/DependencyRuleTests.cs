using AwesomeAssertions;
using NetArchTest.Rules;

namespace FreshOrbit.ArchitectureTests;

/// <summary>Type-level dependency rules (CLAUDE.md rule 2), checked on the compiled assemblies.</summary>
public sealed class DependencyRuleTests
{
    private static readonly string[] Frameworks = ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "FreshOrbit.SharedKernel.EntityFramework", "FreshOrbit.SharedKernel.Web"];
    // D30: a Domain may use its own Public project (to raise the events other modules consume).
    private static readonly string[] NonDomainLayers = ["Application", "Infrastructure"];
    private static readonly string[] NonPublicLayers = ["Domain", "Application", "Infrastructure"];

    public static TheoryData<string> Modules => ModuleCatalog.ModuleData;

    [Theory]
    [MemberData(nameof(Modules))]
    public void Every_module_has_its_four_projects(string module)
    {
        foreach (var layer in ModuleCatalog.Layers)
        {
            var load = () => ModuleCatalog.Load(module, layer);
            load.Should().NotThrow($"{module}.{layer} must exist");
        }
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_depends_only_on_the_shared_kernel_and_its_public(string module)
    {
        var forbidden = Frameworks
            .Concat(ModuleCatalog.Modules.Where(m => m != module).Select(m => $"FreshOrbit.{m}."))
            .Concat(NonDomainLayers.Select(l => ModuleCatalog.Namespace(module, l)))
            .ToArray();

        AssertNoDependency(module, "Domain", forbidden);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Public_depends_only_on_the_shared_kernel(string module)
    {
        var forbidden = Frameworks
            .Concat(ModuleCatalog.Modules.Where(m => m != module).Select(m => $"FreshOrbit.{m}."))
            .Concat(NonPublicLayers.Select(l => ModuleCatalog.Namespace(module, l)))
            .ToArray();

        AssertNoDependency(module, "Public", forbidden);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void No_layer_depends_on_another_modules_application_or_infrastructure(string module)
    {
        var forbidden = ModuleCatalog.Modules.Where(m => m != module)
            .SelectMany(m => new[] { ModuleCatalog.Namespace(m, "Application"), ModuleCatalog.Namespace(m, "Infrastructure") })
            .ToArray();

        foreach (var layer in ModuleCatalog.Layers)
        {
            AssertNoDependency(module, layer, forbidden);
        }
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Only_event_classes_of_another_modules_domain_are_used(string module)
    {
        foreach (var other in ModuleCatalog.Modules.Where(m => m != module))
        {
            var nonEventTypes = ModuleCatalog.Load(other, "Domain").GetTypes()
                .Where(t => t.Namespace is not null && !t.Namespace.EndsWith(".Events", StringComparison.Ordinal) && !t.Namespace.Contains(".Events.", StringComparison.Ordinal))
                .Select(t => t.FullName!)
                .ToArray();
            if (nonEventTypes.Length == 0)
            {
                continue;
            }

            foreach (var layer in ModuleCatalog.Layers)
            {
                AssertNoDependency(module, layer, nonEventTypes);
            }
        }
    }

    private static void AssertNoDependency(string module, string layer, string[] forbidden)
    {
        var result = Types.InAssembly(ModuleCatalog.Load(module, layer))
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"{module}.{layer} must not depend on {string.Join(", ", forbidden)}; offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
