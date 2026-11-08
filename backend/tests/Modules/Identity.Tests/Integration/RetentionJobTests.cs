using AwesomeAssertions;
using FreshOrbit.E1Testing;
using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.Identity.Domain.Otp;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.Identity.Infrastructure;
using FreshOrbit.Identity.Infrastructure.Jobs;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Identity.Tests.Integration;

/// <summary>SEC-13 / D77 retention purge of OTP challenges and staff login failures (D334).</summary>
public sealed class RetentionJobTests(E1Database database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Otp_challenges_older_than_24_hours_are_deleted_and_newer_ones_kept()
    {
        var now = DateTimeOffset.UtcNow;
        var phone = NewPhone();
        var old = OtpChallenge.Create(Guid.CreateVersion7(), phone, OtpPurpose.Login, "hash-old", now.AddHours(-25), TimeSpan.FromMinutes(5), "203.0.113.9");
        var recent = OtpChallenge.Create(Guid.CreateVersion7(), phone, OtpPurpose.Login, "hash-new", now.AddHours(-23), TimeSpan.FromMinutes(5), "203.0.113.9");
        await using (var db = database.CreateDbContext())
        {
            db.AddRange(old, recent);
            await db.SaveChangesAsync(Ct);
        }

        await RunAsync(now);

        await using var read = database.CreateDbContext();
        var left = await read.Set<OtpChallenge>().AsNoTracking().Where(c => c.Phone == phone).Select(c => c.Id).ToListAsync(Ct);
        left.Should().Equal(recent.Id);
    }

    [Fact]
    public async Task Staff_login_failures_older_than_one_day_are_deleted_and_newer_ones_kept()
    {
        var now = DateTimeOffset.UtcNow;
        var email = $"retention-{Guid.NewGuid():N}@example.test";
        var old = StaffLoginFailure.Record(email, "198.51.100.7", now.AddDays(-1).AddMinutes(-1));
        var recent = StaffLoginFailure.Record(email, "198.51.100.7", now.AddHours(-1));
        await using (var db = database.CreateDbContext())
        {
            db.AddRange(old, recent);
            await db.SaveChangesAsync(Ct);
        }

        await RunAsync(now);

        await using var read = database.CreateDbContext();
        var hash = StaffLoginFailure.HashEmail(email);
        var left = await read.Set<StaffLoginFailure>().AsNoTracking().Where(f => f.EmailHash == hash).Select(f => f.Id).ToListAsync(Ct);
        left.Should().Equal(recent.Id);
    }

    [Fact]
    public void The_purge_is_registered_as_a_daily_recurring_job()
    {
        var services = new ServiceCollection();
        new IdentityModule().Register(services, new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var job = provider.GetServices<IRecurringJobRegistration>().SelectMany(r => r.GetJobs()).Single(j => j.Id == IdentityRetentionJob.JobId);

        job.Cron.Should().Be("20 2 * * *");
        job.JobType.Should().Be<IdentityRetentionJob>();
        services.Should().Contain(d => d.ServiceType == typeof(IdentityRetentionJob) && d.Lifetime == ServiceLifetime.Scoped);
    }

    private async Task RunAsync(DateTimeOffset now)
    {
        await using var db = database.CreateDbContext();
        await new IdentityRetentionJob(db, new IdentitySettings(), new TestClock(now)).ExecuteAsync(Ct);
    }

    private static string NewPhone() => "+919" + Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
