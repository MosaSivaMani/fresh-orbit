using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.E1Testing;
using FreshOrbit.Identity.Application.Bootstrap;
using FreshOrbit.Identity.Domain.Staff;
using FreshOrbit.SharedKernel.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Identity.Tests.Integration;

public sealed class StaffTests(E1Database database) : E1ApiTestBase(database)
{
    // The one bootstrapped admin of the shared test database (StaffLockoutTests uses it too).
    internal const string AdminEmail = "first.admin@freshorbit.test";
    internal const string AdminPassword = "Orbit-Admin-Secret-42";

    [Fact]
    public async Task Staff_without_the_permission_gets_403_and_customers_cannot_use_admin()
    {
        using var packer = Factory.CreateStaffClient(["orders.view"]);
        (await packer.GetAsync("/api/v1/admin/staff", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await packer.GetAsync("/api/v1/admin/roles", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await packer.GetAsync("/api/v1/admin/settings/business-profile", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var customer = Factory.CreateCustomerClient();
        (await customer.GetAsync("/api/v1/admin/staff", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var anonymous = Factory.CreateClient();
        (await anonymous.GetAsync("/api/v1/admin/staff", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var manager = Factory.CreateStaffClient(["staff.manage"]);
        (await manager.GetAsync("/api/v1/admin/staff", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task First_admin_signs_in_and_the_last_admin_cannot_lose_the_admin_role()
    {
        var adminStaffId = await EnsureFirstAdminAsync();
        using var anonymous = Factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/v1/admin/auth/login", new { email = AdminEmail, password = AdminPassword }, Ct);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await ReadJsonAsync(login);
        session.GetProperty("staff").GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Should().Contain("staff.manage");

        using var admin = Factory.CreateClient(session.GetProperty("accessToken").GetString()!);
        var roles = await ReadJsonAsync(await admin.GetAsync("/api/v1/admin/roles", Ct));
        var packerRoleId = roles.GetProperty("items").EnumerateArray().First(r => r.GetProperty("code").GetString() == "PACKER").GetProperty("id").GetGuid();

        var current = await admin.GetAsync($"/api/v1/admin/staff/{adminStaffId}", Ct);
        current.StatusCode.Should().Be(HttpStatusCode.OK);
        var demote = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/staff/{adminStaffId}")
        {
            Content = JsonContent.Create(new { fullName = "First Admin", roleIds = new[] { packerRoleId }, allLocations = true, locationIds = Array.Empty<Guid>(), isActive = true }),
        };
        demote.Headers.IfMatch.Add(current.Headers.ETag!);
        var refused = await admin.SendAsync(demote, Ct);

        (await ErrorCodeAsync(refused)).Should().Be("LAST_ADMIN");
        var deactivate = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/staff/{adminStaffId}")
        {
            Content = JsonContent.Create(new { fullName = "First Admin", roleIds = Array.Empty<Guid>(), allLocations = true, locationIds = Array.Empty<Guid>(), isActive = false }),
        };
        deactivate.Headers.IfMatch.Add(current.Headers.ETag!);
        var refusedDeactivation = await admin.SendAsync(deactivate, Ct);
        refusedDeactivation.IsSuccessStatusCode.Should().BeFalse();
        (await QueryAsync(db => db.Set<StaffMember>().SingleAsync(s => s.Id == adminStaffId, Ct))).IsActive.Should().BeTrue();

        // A second admin cannot be bootstrapped.
        var second = async () => await InScopeAsync(p => p.GetRequiredService<IFirstAdminService>()
            .CreateFirstAdminAsync(new CreateFirstAdminCommand("other.admin@freshorbit.test", "Other", AdminPassword), Ct));
        (await second.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("ADMIN_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Tax_manage_is_seeded_for_admin_and_finance_only()
    {
        var roles = await QueryAsync(db => db.Set<Domain.Roles.Role>().AsNoTracking().Where(r => r.IsSystem).ToListAsync(Ct));

        roles.Where(r => r.PermissionCodes.Contains("tax.manage")).Select(r => r.Code).Should().BeEquivalentTo(["ADMIN", "FINANCE"]);
        roles.Single(r => r.Code == "STORE_MANAGER").PermissionCodes.Should().Contain("pricing.manage");
    }

    private async Task<Guid> EnsureFirstAdminAsync()
    {
        try
        {
            return await InScopeAsync(p => p.GetRequiredService<IFirstAdminService>()
                .CreateFirstAdminAsync(new CreateFirstAdminCommand(AdminEmail, "First Admin", AdminPassword), Ct));
        }
        catch (DomainException exception) when (exception.Code == "ADMIN_ALREADY_EXISTS")
        {
            var staff = await QueryAsync(db => db.Set<StaffMember>().AsNoTracking().ToListAsync(Ct));
            return staff.Single(s => s.FullName == "First Admin").Id;
        }
    }
}
