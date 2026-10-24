using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Audit.Domain.Entries;
using FreshOrbit.E1Testing;
using FreshOrbit.Identity.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Audit.Tests.Integration;

/// <summary>
/// SEC-7 / AUD-R6: admin writes are audited in the same SaveChanges by the Audit module's interceptor. Each test uses
/// its own staff user id and filters the audit list by it (the database is shared by the assembly).
/// </summary>
public sealed class AdminWriteAuditTests(E1Database database) : E1ApiTestBase(database)
{
    private const string Redacted = "[REDACTED]";

    private static readonly string[] OrdersView = ["orders.view"];

    private static readonly string[] UnknownPermission = ["no.such.permission"];

    [Fact]
    public async Task Admin_write_records_actor_action_entity_and_redacts_sensitive_fields()
    {
        var managerId = Guid.CreateVersion7();
        using var manager = Factory.CreateStaffClient(["staff.manage", "audit.view"], userId: managerId);
        var email = $"audit.{Guid.NewGuid():N}@freshorbit.test";
        var created = await manager.PostAsJsonAsync("/api/v1/admin/staff", new
        {
            email,
            fullName = "Audit Packer",
            roleIds = new[] { await RoleIdAsync(manager, "PACKER") },
            allLocations = true,
            temporaryPassword = "Audit-Temp-Pass-91",
        }, Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var staff = await ReadJsonAsync(created);
        var userId = staff.GetProperty("userId").GetGuid();

        // StaffService records STAFF_CREATED for the staff member itself (IAuditApi); the generic recorder adds the
        // other changed entities of that save (the user) under the same action, and never the staff member again.
        var createdEntries = await EntriesAsync(manager, managerId, "STAFF_CREATED");
        createdEntries.Should().ContainSingle(e => e.GetProperty("entityType").GetString() == "STAFF_MEMBER")
            .Which.GetProperty("entityId").GetString().Should().Be(staff.GetProperty("id").GetString());
        createdEntries.Should().ContainSingle(e => e.GetProperty("entityType").GetString() == "USER")
            .Which.GetProperty("after").GetProperty("passwordHash").GetString().Should().Be(Redacted);

        // The new staff member signs in (exempt) and changes their password: audited by the generic recorder.
        using var anonymous = Factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/v1/admin/auth/login", new { email, password = "Audit-Temp-Pass-91" }, Ct);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        using var packer = Factory.CreateClient((await ReadJsonAsync(login)).GetProperty("accessToken").GetString()!);
        (await EntriesAsync(manager, userId, action: null)).Should().BeEmpty("login is exempt");

        var changed = await packer.PostAsJsonAsync("/api/v1/admin/auth/password", new { currentPassword = "Audit-Temp-Pass-91", newPassword = "Audit-Own-Pass-93x" }, Ct);
        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var entries = await EntriesAsync(manager, userId, "STAFF_PASSWORD_CHANGED");
        var user = entries.Should().ContainSingle(e => e.GetProperty("entityType").GetString() == "USER").Subject;
        user.GetProperty("entityId").GetString().Should().Be(userId.ToString());
        user.GetProperty("actorType").GetString().Should().Be("STAFF");
        user.GetProperty("actorId").GetGuid().Should().Be(userId);
        user.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
        user.GetProperty("before").GetProperty("passwordHash").GetString().Should().Be(Redacted);
        user.GetProperty("after").GetProperty("passwordHash").GetString().Should().Be(Redacted);
        entries.Should().Contain(e => e.GetProperty("entityType").GetString() == "REFRESH_TOKEN", "the revoked sessions are part of the same save");
        string.Concat(entries.Select(e => e.GetRawText())).Should().NotContain("AQAAAA", "no password hash (PBKDF2 v3 prefix) may reach the audit trail");
    }

    [Fact]
    public async Task Explicit_record_covers_only_its_entity_and_the_rest_of_the_save_gets_generic_entries()
    {
        var managerId = Guid.CreateVersion7();
        using var manager = Factory.CreateStaffClient(["staff.manage", "audit.view"], userId: managerId);
        var created = await manager.PostAsJsonAsync("/api/v1/admin/staff", new
        {
            email = $"reset.{Guid.NewGuid():N}@freshorbit.test",
            fullName = "Reset Packer",
            roleIds = new[] { await RoleIdAsync(manager, "PACKER") },
            allLocations = true,
            temporaryPassword = "Reset-Temp-Pass-81",
        }, Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var staff = await ReadJsonAsync(created);
        var staffId = staff.GetProperty("id").GetString();
        var userId = staff.GetProperty("userId").GetString();

        using var reset = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/staff/{staffId}/reset-password")
        {
            Content = JsonContent.Create(new { temporaryPassword = "Reset-New-Pass-82x" }),
        };
        reset.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{staff.GetProperty("version").GetString()}\""));
        (await manager.SendAsync(reset, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        // One save: StaffService's explicit STAFF_MEMBER record, plus the USER password change from the interceptor.
        var entries = await EntriesAsync(manager, managerId, "STAFF_PASSWORD_RESET");
        entries.Where(e => e.GetProperty("entityType").GetString() == "STAFF_MEMBER" && e.GetProperty("entityId").GetString() == staffId)
            .Should().ContainSingle("the explicit record covers the staff member, so it is not recorded twice");
        var user = entries.Should().ContainSingle(e => e.GetProperty("entityType").GetString() == "USER").Subject;
        user.GetProperty("entityId").GetString().Should().Be(userId);
        user.GetProperty("before").GetProperty("passwordHash").GetString().Should().Be(Redacted);
        user.GetProperty("after").GetProperty("passwordHash").GetString().Should().Be(Redacted);
        string.Concat(entries.Select(e => e.GetRawText())).Should().NotContain("AQAAAA");
    }

    [Fact]
    public async Task Admin_update_records_before_and_after_of_the_changed_properties_only()
    {
        var actorId = Guid.CreateVersion7();
        using var manager = Factory.CreateStaffClient(["staff.manage", "audit.view"], userId: actorId);
        var code = UniqueRoleCode();
        var created = await manager.PostAsJsonAsync("/api/v1/admin/roles", new { code, name = "Night packer", permissions = OrdersView }, Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var role = await ReadJsonAsync(created);
        var roleId = role.GetProperty("id").GetGuid();

        using var update = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/roles/{roleId}")
        {
            Content = JsonContent.Create(new { name = "Late packer", permissions = OrdersView }),
        };
        update.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{role.GetProperty("version").GetString()}\""));
        (await manager.SendAsync(update, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        var entry = (await EntriesAsync(manager, actorId, "ROLE_UPDATED")).Should().ContainSingle(e => e.GetProperty("entityId").GetString() == roleId.ToString()).Subject;
        entry.GetProperty("entityType").GetString().Should().Be("ROLE");
        entry.GetProperty("before").GetProperty("name").GetString().Should().Be("Night packer");
        entry.GetProperty("after").GetProperty("name").GetString().Should().Be("Late packer");
        entry.GetProperty("after").TryGetProperty("code", out _).Should().BeFalse("an update records only the changed properties");
        (await EntriesAsync(manager, actorId, "ROLE_CREATED")).Should().Contain(e => e.GetProperty("entityId").GetString() == roleId.ToString());
    }

    [Fact]
    public async Task Refused_admin_write_records_nothing()
    {
        var actorId = Guid.CreateVersion7();
        using var manager = Factory.CreateStaffClient(["staff.manage", "audit.view"], userId: actorId);

        var refused = await manager.PostAsJsonAsync("/api/v1/admin/roles", new { code = UniqueRoleCode(), name = "Bad", permissions = UnknownPermission }, Ct);

        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorCodeAsync(refused)).Should().Be("PERMISSION_UNKNOWN");
        (await EntriesAsync(manager, actorId, action: null)).Should().BeEmpty();
    }

    [Fact]
    public async Task Customer_write_records_nothing()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var customer = Factory.CreateClient(session.AccessToken);

        var logout = await customer.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = session.RefreshToken }, Ct);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var count = await QueryAsync(db => db.Set<AuditEntry>().CountAsync(e => e.ActorId == session.UserId, Ct));
        count.Should().Be(0);
    }

    [Fact]
    public async Task Audit_entry_commits_or_rolls_back_with_the_change()
    {
        Factory.AdditionalServices = services => services.AddScoped<IInterceptor, FailAfterAuditInsert>();
        var actorId = Guid.CreateVersion7();
        using var manager = Factory.CreateStaffClient(["staff.manage"], userId: actorId);
        var code = UniqueRoleCode();

        var failed = await manager.PostAsJsonAsync("/api/v1/admin/roles", new { code, name = "Never saved", permissions = OrdersView }, Ct);

        failed.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await QueryAsync(db => db.Set<Role>().CountAsync(r => r.Code == code, Ct))).Should().Be(0);
        (await QueryAsync(db => db.Set<AuditEntry>().CountAsync(e => e.ActorId == actorId, Ct))).Should().Be(0);
    }

    private static string UniqueRoleCode() => "AUDIT_" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    private static async Task<Guid> RoleIdAsync(HttpClient client, string code)
    {
        var roles = await ReadJsonAsync(await client.GetAsync("/api/v1/admin/roles", Ct));
        return roles.GetProperty("items").EnumerateArray().First(r => r.GetProperty("code").GetString() == code).GetProperty("id").GetGuid();
    }

    private static async Task<List<JsonElement>> EntriesAsync(HttpClient viewer, Guid actorId, string? action)
    {
        var url = $"/api/v1/admin/audit?actorId={actorId}&pageSize=100" + (action is null ? string.Empty : $"&action={action}");
        var response = await viewer.GetAsync(url, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return [.. (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray()];
    }

    /// <summary>Fails the save after the database has run the audit insert, so EF must roll the whole save back.</summary>
    private sealed class FailAfterAuditInsert : DbCommandInterceptor
    {
        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
            => IsAuditInsert(command) ? throw new InvalidOperationException("Simulated failure after the audit insert.") : result;

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
            => IsAuditInsert(command) ? throw new InvalidOperationException("Simulated failure after the audit insert.") : ValueTask.FromResult(result);

        private static bool IsAuditInsert(DbCommand command) => command.CommandText.Contains("INSERT INTO audit.entries", StringComparison.OrdinalIgnoreCase);
    }
}
