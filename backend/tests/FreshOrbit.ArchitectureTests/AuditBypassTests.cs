using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace FreshOrbit.ArchitectureTests;

/// <summary>
/// SEC-7 / AUD-R6: admin writes are audited by a SaveChanges interceptor, which sees only tracked changes. Set-based
/// writes (<c>ExecuteUpdate</c>, <c>ExecuteDelete</c>, raw SQL) bypass it, so module code may use them only in the
/// places listed here, none of which runs in an admin request.
/// </summary>
public sealed partial class AuditBypassTests
{
    /// <summary>Module source file (relative to backend/src/Modules) → why its set-based write is not an admin write.</summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal)
    {
        ["Notifications/Notifications.Infrastructure/Persistence/Repositories/NotificationsRepository.cs"] = "Customer marks own in-app messages read (/me).",
        ["Reporting/Reporting.Infrastructure/Jobs/RefreshReportsJob.cs"] = "Worker job: advisory lock and materialized view refresh.",
        ["Payments/Payments.Infrastructure/Jobs/PaymentsJobs.cs"] = "Worker job: advisory lock release.",
        ["Delivery/Delivery.Infrastructure/Seeding/DeliverySampleDataLoader.cs"] = "Migrations host --purge-sample: removes unbooked slots of sample templates.",
    };

    [Fact]
    public void Set_based_writes_appear_only_where_allowed()
    {
        var modules = Path.Combine(ModuleCatalog.BackendDirectory, "src", "Modules");
        var offenders = Directory.EnumerateFiles(modules, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => SetBasedWrite().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(modules, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(path => !Allowed.ContainsKey(path))
            .ToList();

        Allowed.Keys.Should().OnlyContain(path => SetBasedWrite().IsMatch(File.ReadAllText(Path.Combine(modules, path))), "a stale allowance must be removed");
        offenders.Should().BeEmpty("a set-based write in an admin command escapes the audit trail; use tracked entities or record it with IAuditApi and list the file here with a reason");
    }

    [GeneratedRegex(@"\b(ExecuteUpdate|ExecuteDelete|ExecuteSql)\w*\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex SetBasedWrite();
}
