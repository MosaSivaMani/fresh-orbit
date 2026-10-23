using System.Xml.Linq;
using AwesomeAssertions;

namespace FreshOrbit.ArchitectureTests;

/// <summary>Project-reference rules (CLAUDE.md rule 2 and the "Uses" column of the architecture doc).</summary>
public sealed class ProjectReferenceRuleTests
{
    public static TheoryData<string> Modules => ModuleCatalog.ModuleData;

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_references_only_the_shared_kernel_and_its_public(string module)
        => References(module, "Domain").Should().OnlyContain(r => r == "FreshOrbit.SharedKernel" || r == $"{module}.Public")
            .And.Contain("FreshOrbit.SharedKernel");

    [Theory]
    [MemberData(nameof(Modules))]
    public void Public_references_only_the_shared_kernel(string module)
        => References(module, "Public").Should().BeEquivalentTo(["FreshOrbit.SharedKernel"]);

    [Theory]
    [MemberData(nameof(Modules))]
    public void Application_references_its_domain_its_public_and_the_public_of_used_modules(string module)
    {
        var allowed = new[] { $"{module}.Domain", $"{module}.Public" }.Concat(ModuleCatalog.Uses[module].Select(u => $"{u}.Public"));

        References(module, "Application").Should().OnlyContain(r => allowed.Contains(r))
            .And.Contain($"{module}.Domain");
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Infrastructure_references_its_application_its_public_and_the_shared_kernel(string module)
        => References(module, "Infrastructure").Should().BeEquivalentTo(
        [
            $"{module}.Application", $"{module}.Public", "FreshOrbit.SharedKernel.EntityFramework", "FreshOrbit.SharedKernel.Web",
        ]);

    [Fact]
    public void Shared_kernel_does_not_reference_entity_framework_or_asp_net()
    {
        var project = Path.Combine(ModuleCatalog.BackendDirectory, "src", "SharedKernel", "FreshOrbit.SharedKernel", "FreshOrbit.SharedKernel.csproj");
        var document = XDocument.Load(project);

        document.Descendants().Where(e => e.Name.LocalName is "PackageReference" or "ProjectReference" or "FrameworkReference").Should().BeEmpty();
    }

    private static List<string> References(string module, string layer)
    {
        var project = Path.Combine(ModuleCatalog.BackendDirectory, "src", "Modules", module, $"{module}.{layer}", $"{module}.{layer}.csproj");
        return [.. XDocument.Load(project).Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/')))];
    }
}
