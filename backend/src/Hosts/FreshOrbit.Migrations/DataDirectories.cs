namespace FreshOrbit.Migrations;

/// <summary>
/// Finds <c>backend/data/reference</c> and <c>backend/data/sample</c>: the configured path
/// (<c>Seeding:ReferenceDataPath</c> / <c>Seeding:SampleDataPath</c>) or else the nearest <c>data/&lt;kind&gt;</c>
/// folder above the current directory or the application directory.
/// </summary>
internal static class DataDirectories
{
    public static string Resolve(IConfiguration configuration, string configurationKey, string kind)
    {
        var configured = configuration[configurationKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Directory.Exists(configured)
                ? Path.GetFullPath(configured)
                : throw new DirectoryNotFoundException($"{configurationKey} points to '{configured}', which does not exist.");
        }

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                foreach (var candidate in new[] { Path.Combine(directory.FullName, "data", kind), Path.Combine(directory.FullName, "backend", "data", kind) })
                {
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        throw new DirectoryNotFoundException($"No data/{kind} folder was found; set {configurationKey}.");
    }
}
