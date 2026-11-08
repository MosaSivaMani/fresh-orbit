using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.Identity.Domain.Otp;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Identity.Infrastructure.Jobs;

/// <summary>
/// Daily retention purge of the Identity module (SEC-13, D77, D334). Two set-based deletes in one transaction, no rows
/// loaded:
/// <list type="bullet">
/// <item><c>identity.otp_challenges</c> (phone and request IP) requested more than <see cref="OtpChallengeRetention"/>
/// ago. The longest OTP limit window is one day (<see cref="IdentitySettings.OtpPerPhonePerDay"/>), so nothing the
/// limits count is removed.</item>
/// <item><c>identity.staff_login_failures</c> older than <see cref="StaffLoginFailureRetention"/>, or than the
/// configured failure window when that is longer, so the per e-mail + IP limit (IDN-R20) never loses a counted row.</item>
/// </list>
/// </summary>
public sealed class IdentityRetentionJob(FreshOrbitDbContext db, IdentitySettings settings, IClock clock) : IRecurringJob
{
    public const string JobId = "identity.purge-retention";

    /// <summary>SEC-13 / privacy policy: OTP requests are kept 24 hours.</summary>
    public static readonly TimeSpan OtpChallengeRetention = TimeSpan.FromHours(24);

    /// <summary>D77: refused staff logins are kept one day.</summary>
    public static readonly TimeSpan StaffLoginFailureRetention = TimeSpan.FromDays(1);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var otpCutoff = now - OtpChallengeRetention;
        var failureWindow = TimeSpan.FromMinutes(settings.StaffLoginFailureWindowMinutes);
        var failureCutoff = now - (failureWindow > StaffLoginFailureRetention ? failureWindow : StaffLoginFailureRetention);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Set<OtpChallenge>().Where(c => c.RequestedAt < otpCutoff).ExecuteDeleteAsync(cancellationToken);
        await db.Set<StaffLoginFailure>().Where(f => f.FailedAt < failureCutoff).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

internal sealed class IdentityJobRegistration : IRecurringJobRegistration
{
    public IEnumerable<RecurringJobDefinition> GetJobs() => [new(IdentityRetentionJob.JobId, "20 2 * * *", typeof(IdentityRetentionJob))];
}
