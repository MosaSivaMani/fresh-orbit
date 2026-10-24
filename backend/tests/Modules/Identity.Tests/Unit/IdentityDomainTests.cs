using AwesomeAssertions;
using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.Identity.Domain.Otp;
using FreshOrbit.Identity.Domain.Roles;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.Identity.Domain.Tokens;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Identity.Tests.Unit;

public sealed class OtpChallengeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);

    private static OtpChallenge NewChallenge() => OtpChallenge.Create(Guid.CreateVersion7(), "+919876543210", OtpPurpose.Login, "good", Now, TimeSpan.FromMinutes(5), null);

    [Fact]
    public void Correct_code_is_accepted_once()
    {
        var challenge = NewChallenge();

        challenge.Verify("good", Now.AddMinutes(1), 5).Should().Be(OtpCheck.Accepted);
        challenge.Verify("good", Now.AddMinutes(1), 5).Should().Be(OtpCheck.Expired);
    }

    [Fact]
    public void Fifth_wrong_code_locks_the_challenge_even_for_the_right_code()
    {
        var challenge = NewChallenge();
        for (var i = 0; i < 4; i++)
        {
            challenge.Verify("bad", Now, 5).Should().Be(OtpCheck.Invalid);
        }

        challenge.Verify("bad", Now, 5).Should().Be(OtpCheck.TooManyAttempts);
        challenge.Verify("good", Now, 5).Should().Be(OtpCheck.TooManyAttempts);
        challenge.AttemptCount.Should().Be(5);
    }

    [Fact]
    public void Code_is_refused_at_expiry()
        => NewChallenge().Verify("good", Now.AddMinutes(5), 5).Should().Be(OtpCheck.Expired);
}

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Rotation_keeps_the_family_and_revokes_the_old_token()
    {
        var first = RefreshToken.Issue(Guid.CreateVersion7(), null, "h1", Now, TimeSpan.FromDays(30), "phone");
        var second = first.Rotate("h2", Now.AddMinutes(1), TimeSpan.FromDays(30));

        second.FamilyId.Should().Be(first.FamilyId);
        first.WasRotated.Should().BeTrue();
        first.IsActive(Now.AddMinutes(1)).Should().BeFalse();
        second.IsActive(Now.AddMinutes(1)).Should().BeTrue();
        second.DeviceLabel.Should().Be("phone");
    }

    [Fact]
    public void Reported_reuse_raises_an_event()
    {
        var token = RefreshToken.Issue(Guid.CreateVersion7(), null, "h1", Now, TimeSpan.FromDays(30), null);
        token.ReportReuse(Now);
        token.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "RefreshTokenReuseDetected");
    }
}

/// <summary>D33 time-based staff lockout (IDN-R12).</summary>
public sealed class StaffLockoutTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);

    private static readonly LockoutPolicy Policy = new IdentitySettings().StaffLockoutPolicy();

    /// <summary>Records wrong passwords at <paramref name="at"/> until the account locks.</summary>
    private static void FailUntilLocked(User user, DateTimeOffset at)
    {
        for (var i = 0; i < Policy.MaxFailures; i++)
        {
            user.RecordFailedPassword(Policy, at);
        }
    }

    [Fact]
    public void Default_policy_is_ten_failures_fifteen_minutes_and_a_day_at_most()
    {
        Policy.MaxFailures.Should().Be(10);
        Policy.FirstLock.Should().Be(TimeSpan.FromMinutes(15));
        Policy.MaxLock.Should().Be(TimeSpan.FromHours(24));
    }

    [Theory]
    [InlineData(1, 15)]
    [InlineData(2, 30)]
    [InlineData(3, 60)]
    [InlineData(4, 120)]
    [InlineData(7, 960)]
    [InlineData(8, 1440)]
    [InlineData(9, 1440)]
    [InlineData(1000, 1440)]
    public void Each_lock_doubles_up_to_24_hours(int lockoutCount, int minutes)
        => Policy.LengthOf(lockoutCount).Should().Be(TimeSpan.FromMinutes(minutes));

    [Fact]
    public void The_tenth_failure_locks_for_15_minutes_and_raises_the_alert_event()
    {
        var user = User.CreateStaff("a@b.in", "hash");
        for (var i = 0; i < 9; i++)
        {
            user.RecordFailedPassword(Policy, Now).Should().BeFalse();
        }

        user.Status.Should().Be(UserStatus.Active);
        user.RecordFailedPassword(Policy, Now).Should().BeTrue();

        user.Status.Should().Be(UserStatus.Locked);
        user.LockedUntil.Should().Be(Now.AddMinutes(15));
        user.LockoutCount.Should().Be(1);
        user.FailedLoginCount.Should().Be(0);
        user.IsLockedAt(Now.AddMinutes(14)).Should().BeTrue();
        var locked = user.DomainEvents.OfType<FreshOrbit.Identity.Public.Events.StaffAccountLocked>().Should().ContainSingle().Subject;
        locked.UserId.Should().Be(user.Id);
        locked.LockedUntil.Should().Be(Now.AddMinutes(15));
        locked.LockoutCount.Should().Be(1);
    }

    [Fact]
    public void Failures_during_a_lock_neither_count_nor_extend_it()
    {
        var user = User.CreateStaff("a@b.in", "hash");
        FailUntilLocked(user, Now);

        for (var i = 0; i < 25; i++)
        {
            user.RecordFailedPassword(Policy, Now.AddMinutes(5)).Should().BeFalse();
        }

        user.LockedUntil.Should().Be(Now.AddMinutes(15));
        user.LockoutCount.Should().Be(1);
        user.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void The_lock_expires_by_time_without_an_administrator()
    {
        var user = User.CreateStaff("a@b.in", "hash");
        FailUntilLocked(user, Now);

        user.ReleaseExpiredLock(Now.AddMinutes(15).AddTicks(-1));
        user.Status.Should().Be(UserStatus.Locked);

        user.ReleaseExpiredLock(Now.AddMinutes(15));
        user.Status.Should().Be(UserStatus.Active);
        user.LockedUntil.Should().BeNull();
        user.LockoutCount.Should().Be(1, "the doubling survives the expiry");
    }

    [Fact]
    public void A_repeat_lock_doubles_and_a_successful_sign_in_resets_the_doubling()
    {
        var user = User.CreateStaff("a@b.in", "hash");
        FailUntilLocked(user, Now);
        var second = Now.AddMinutes(15);
        user.ReleaseExpiredLock(second);
        FailUntilLocked(user, second);

        user.LockoutCount.Should().Be(2);
        user.LockedUntil.Should().Be(second.AddMinutes(30));

        user.ReleaseExpiredLock(second.AddMinutes(30));
        user.RecordPasswordSignIn(second.AddMinutes(31));
        user.LockoutCount.Should().Be(0);
        user.FailedLoginCount.Should().Be(0);

        FailUntilLocked(user, second.AddMinutes(40));
        user.LockedUntil.Should().Be(second.AddMinutes(55));
    }

    [Fact]
    public void A_locked_row_without_an_end_time_counts_as_expired()
    {
        var user = User.CreateStaff("a@b.in", "hash");
        FailUntilLocked(user, Now);
        typeof(User).GetProperty(nameof(User.LockedUntil))!.SetValue(user, null);

        user.IsLockedAt(Now).Should().BeFalse();
        user.ReleaseExpiredLock(Now);
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Admin_reset_unlocks_at_once_and_resets_the_doubling()
    {
        var user = User.CreateStaff("a@b.in", "hash");
        FailUntilLocked(user, Now);
        user.Invoking(u => u.EnsureCanSignIn()).Should().Throw<DomainException>().Which.Code.Should().Be(IdentityErrors.AccountLocked);

        user.ResetPassword("new-hash");

        user.Status.Should().Be(UserStatus.Active);
        user.FailedLoginCount.Should().Be(0);
        user.LockoutCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
    }

    [Fact]
    public void A_disabled_account_never_locks()
    {
        var user = User.CreateStaff("a@b.in", "hash");
        user.Disable();
        FailUntilLocked(user, Now);

        user.Status.Should().Be(UserStatus.Disabled);
        user.FailedLoginCount.Should().Be(0);
        user.DomainEvents.Should().BeEmpty();
    }
}

public sealed class UserTests
{

    [Fact]
    public void Anonymised_user_cannot_be_enabled_again()
    {
        var user = User.RegisterCustomer("+919876543210", DateTimeOffset.UtcNow);
        user.Anonymise(DateTimeOffset.UtcNow);

        user.Phone.Should().BeNull();
        user.Invoking(u => u.Enable()).Should().Throw<ConflictException>();
    }

    [Theory]
    [InlineData("short")]
    [InlineData("password1234")]
    [InlineData("aaaaaaaaaaaaaaaa")]
    public void Weak_passwords_are_refused(string password)
        => FluentActions.Invoking(() => PasswordPolicy.Ensure(password, "password")).Should().Throw<DomainException>()
            .Which.Code.Should().Be(IdentityErrors.PasswordTooWeak);

    [Fact]
    public void A_long_uncommon_password_is_accepted()
        => FluentActions.Invoking(() => PasswordPolicy.Ensure("Mango-River-42-Kite", "password")).Should().NotThrow();
}

public sealed class RoleTests
{
    [Fact]
    public void Admin_permissions_cannot_be_changed()
    {
        var admin = Role.Create(IdentityErrors.AdminRoleCode, "Administrator", true, ["orders.view", "staff.manage"]);

        admin.Invoking(r => r.Update("Administrator", ["orders.view"])).Should().Throw<DomainException>()
            .Which.Code.Should().Be(IdentityErrors.SystemRoleReadOnly);
    }

    [Fact]
    public void System_roles_cannot_be_deleted_but_custom_roles_can()
    {
        Role.Create("PACKER", "Packer", true, []).Invoking(r => r.EnsureDeletable()).Should().Throw<DomainException>();
        Role.Create("NIGHT_SHIFT", "Night shift", false, []).Invoking(r => r.EnsureDeletable()).Should().NotThrow();
    }
}
