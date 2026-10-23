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

public sealed class UserTests
{
    [Fact]
    public void Staff_is_locked_after_max_failed_passwords_and_reset_unlocks()
    {
        var user = User.CreateStaff("a@b.in", "hash");
        for (var i = 0; i < 10; i++)
        {
            user.RecordFailedPassword(10);
        }

        user.Status.Should().Be(UserStatus.Locked);
        user.Invoking(u => u.EnsureCanSignIn()).Should().Throw<DomainException>().Which.Code.Should().Be(IdentityErrors.AccountLocked);

        user.ResetPassword("new-hash");
        user.Status.Should().Be(UserStatus.Active);
        user.FailedLoginCount.Should().Be(0);
    }

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
