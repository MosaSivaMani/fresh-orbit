using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.E1Testing;
using FreshOrbit.Identity.Application.Bootstrap;
using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.Identity.Domain.Staff;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.Notifications.Domain.Messages;
using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Identity.Tests.Integration;

/// <summary>D33 time-based staff lockout, IDN-R20 per email + IP limit, the admin alert and the break-glass command.</summary>
public sealed class StaffLockoutTests : E1ApiTestBase
{
    private const string WrongPassword = "Wrong-Password-000";
    private const string IpA = "198.51.100.1";
    private const string IpB = "198.51.100.2";
    private const string IpC = "198.51.100.3";

    public StaffLockoutTests(E1Database database)
        : base(database)
        => Factory.AdditionalServices = services => services.Replace(ServiceDescriptor.Scoped<IRequestContext, HeaderRequestContext>());

    [Fact]
    public async Task A_locked_account_with_the_right_password_gets_invalid_credentials()
    {
        var (email, password) = await CreatePackerAsync();
        await LockAsync(email);

        var login = await LoginAsync(email, password, IpC);

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ErrorCodeAsync(login)).Should().Be("INVALID_CREDENTIALS");
        var user = await FindUserAsync(email);
        user.Status.Should().Be(UserStatus.Locked);
        user.LockedUntil.Should().BeCloseTo(Factory.Clock.UtcNow.AddMinutes(15), TimeSpan.FromMicroseconds(1));
        user.LockoutCount.Should().Be(1);
    }

    [Fact]
    public async Task The_lock_expires_by_time_and_a_repeat_lock_lasts_twice_as_long()
    {
        var (email, password) = await CreatePackerAsync();
        await LockAsync(email);

        Factory.Clock.Advance(TimeSpan.FromMinutes(14));
        (await LoginAsync(email, password, IpC)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await LockAsync(email);
        (await FindUserAsync(email)).LockedUntil.Should().BeCloseTo(Factory.Clock.UtcNow.AddMinutes(30), TimeSpan.FromMicroseconds(1));

        Factory.Clock.Advance(TimeSpan.FromMinutes(30));
        var login = await LoginAsync(email, password, IpC);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await FindUserAsync(email);
        user.Status.Should().Be(UserStatus.Active);
        user.LockoutCount.Should().Be(0);
    }

    [Fact]
    public async Task Five_failures_from_one_ip_for_one_email_give_429_while_other_ips_still_sign_in()
    {
        var (email, password) = await CreatePackerAsync();
        for (var i = 0; i < 5; i++)
        {
            (await LoginAsync(email, WrongPassword, IpA)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var limited = await LoginAsync(email, password, IpA);

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var problem = await ReadJsonAsync(limited);
        problem.GetProperty("code").GetString().Should().Be("RATE_LIMITED");
        problem.GetProperty("retryAfterSeconds").GetInt32().Should().BeInRange(1, 15 * 60);
        limited.Headers.RetryAfter.Should().NotBeNull();

        (await LoginAsync(email, password, IpB)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await LoginAsync(email, password, IpA)).StatusCode.Should().Be(HttpStatusCode.OK, "a successful sign-in clears the failures before it");

        var unknown = $"nobody.{Guid.NewGuid():N}@freshorbit.test";
        for (var i = 0; i < 5; i++)
        {
            (await LoginAsync(unknown, WrongPassword, IpA)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await LoginAsync(unknown, WrongPassword, IpA)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "unknown emails are limited the same way");
    }

    [Fact]
    public async Task A_lock_raises_an_event_that_emails_the_administrators()
    {
        var adminStaffId = await EnsureFirstAdminAsync();
        var adminUserId = await QueryAsync(db => db.Set<StaffMember>().AsNoTracking().IgnoreAutoIncludes().Where(s => s.Id == adminStaffId).Select(s => s.UserId).SingleAsync(Ct));
        var (email, _) = await CreatePackerAsync();
        await LockAsync(email);
        var user = await FindUserAsync(email);

        var outbox = (await QueryAsync(db => db.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.EventType.Contains(".StaffAccountLocked,"))
            .ToListAsync(Ct))).Where(m => m.Payload.Contains(user.Id.ToString(), StringComparison.Ordinal)).ToList();
        outbox.Should().ContainSingle();

        var dispatcher = Factory.Services.GetRequiredService<OutboxDispatcher>();
        while (await dispatcher.DispatchBatchAsync(Ct) > 0)
        {
        }

        var after = await QueryAsync(db => db.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.Id == outbox[0].Id, Ct));
        after.ProcessedAt.Should().NotBeNull(after.Error);
        var dedupePrefix = outbox[0].Id.ToString("N") + ":";
        var messages = await QueryAsync(db => db.Set<Message>().AsNoTracking().Where(m => m.TemplateCode == "STAFF_ACCOUNT_LOCKED" && m.DedupeKey!.StartsWith(dedupePrefix)).ToListAsync(Ct));
        messages.Should().Contain(m => m.UserId == adminUserId && m.Channel == Notifications.Domain.Shared.Channel.Email);
        messages.Should().NotContain(m => m.UserId == user.Id);
        messages.Single(m => m.UserId == adminUserId).Payload["staffEmail"].Should().Be(email);
    }

    [Fact]
    public async Task When_the_only_admin_is_locked_create_admin_is_refused_and_access_returns_when_the_lock_expires()
    {
        await EnsureFirstAdminAsync();
        await LockAsync(StaffTests.AdminEmail);
        (await LoginAsync(StaffTests.AdminEmail, StaffTests.AdminPassword, IpC)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // D18 / IDN-R19: the break-glass command refuses while an active staff member holds ADMIN, locked or not.
        var createAdmin = async () => await InScopeAsync(p => p.GetRequiredService<IFirstAdminService>()
            .CreateFirstAdminAsync(new CreateFirstAdminCommand($"rescue.{Guid.NewGuid():N}@freshorbit.test", "Rescue Admin", "Rescue-Admin-Secret-77"), Ct));
        (await createAdmin.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("ADMIN_ALREADY_EXISTS");

        Factory.Clock.Advance(TimeSpan.FromMinutes(15));
        (await LoginAsync(StaffTests.AdminEmail, StaffTests.AdminPassword, IpC)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Ten wrong passwords, five from each of two IPs (the per email + IP limit allows five).</summary>
    private async Task LockAsync(string email)
    {
        foreach (var ip in new[] { IpA, IpB })
        {
            for (var i = 0; i < 5; i++)
            {
                var wrong = await LoginAsync(email, WrongPassword, ip);
                wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                (await ErrorCodeAsync(wrong)).Should().Be("INVALID_CREDENTIALS");
            }
        }

        (await FindUserAsync(email)).Status.Should().Be(UserStatus.Locked);
    }

    private async Task<HttpResponseMessage> LoginAsync(string email, string password, string ip)
    {
        using var client = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add(HeaderRequestContext.Header, ip);
        return await client.SendAsync(request, Ct);
    }

    private async Task<(string Email, string Password)> CreatePackerAsync()
    {
        using var manager = Factory.CreateStaffClient(["staff.manage"]);
        var roles = await ReadJsonAsync(await manager.GetAsync("/api/v1/admin/roles", Ct));
        var packerRoleId = roles.GetProperty("items").EnumerateArray().First(r => r.GetProperty("code").GetString() == "PACKER").GetProperty("id").GetGuid();
        var email = $"packer.{Guid.NewGuid():N}@freshorbit.test";
        const string password = "Packer-Temp-Pass-77";
        var created = await manager.PostAsJsonAsync("/api/v1/admin/staff", new { email, fullName = "Packer", roleIds = new[] { packerRoleId }, allLocations = true, temporaryPassword = password }, Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return (email, password);
    }

    private Task<User> FindUserAsync(string email)
        => QueryAsync(db => db.Set<User>().AsNoTracking().SingleAsync(u => u.Email == email, Ct));

    private async Task<Guid> EnsureFirstAdminAsync()
    {
        try
        {
            return await InScopeAsync(p => p.GetRequiredService<IFirstAdminService>()
                .CreateFirstAdminAsync(new CreateFirstAdminCommand(StaffTests.AdminEmail, "First Admin", StaffTests.AdminPassword), Ct));
        }
        catch (DomainException exception) when (exception.Code == "ADMIN_ALREADY_EXISTS")
        {
            var staff = await QueryAsync(db => db.Set<StaffMember>().AsNoTracking().ToListAsync(Ct));
            return staff.Single(s => s.FullName == "First Admin").Id;
        }
    }

    /// <summary>The client IP taken from a test header, so one test can act from several IPs.</summary>
    private sealed class HeaderRequestContext(IHttpContextAccessor accessor) : IRequestContext
    {
        public const string Header = "X-Test-Client-Ip";

        public string? ClientIp => accessor.HttpContext?.Request.Headers[Header].FirstOrDefault();
    }
}
