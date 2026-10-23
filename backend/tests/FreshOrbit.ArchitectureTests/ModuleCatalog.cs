using System.Reflection;

namespace FreshOrbit.ArchitectureTests;

/// <summary>The module table of docs/architecture/README.md: each module and the modules whose .Public it uses.</summary>
public static class ModuleCatalog
{
    public static readonly IReadOnlyDictionary<string, string[]> Uses = new Dictionary<string, string[]>
    {
        ["Identity"] = [],
        ["Customers"] = ["Identity", "Catalog"],
        ["Locations"] = [],
        ["Catalog"] = [],
        ["Pricing"] = ["Catalog", "Locations"],
        ["Inventory"] = ["Catalog", "Locations"],
        ["Cart"] = ["Catalog", "Pricing", "Inventory", "Locations", "Promotions", "Settings", "Customers", "Ordering"],
        ["Promotions"] = ["Catalog", "Locations"],
        ["Ordering"] = ["Cart", "Catalog", "Pricing", "Inventory", "Delivery", "Promotions", "Locations", "Customers", "Settings", "Payments"],
        ["Payments"] = ["Ordering", "Delivery", "Identity"],
        ["Delivery"] = ["Locations"],
        ["Notifications"] = ["Identity"],
        ["Reporting"] = [],
        ["Audit"] = [],
        ["Settings"] = [],
    };

    public static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Public"];

    public static IEnumerable<string> Modules => Uses.Keys;

    public static TheoryData<string> ModuleData => [.. Modules];

    public static string Namespace(string module, string layer) => $"FreshOrbit.{module}.{layer}";

    public static Assembly Load(string module, string layer) => Assembly.Load(Namespace(module, layer));

    public static string BackendDirectory
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FreshOrbit.slnx")))
                {
                    return directory.FullName;
                }
            }

            throw new DirectoryNotFoundException("FreshOrbit.slnx was not found above the test output directory.");
        }
    }
}
