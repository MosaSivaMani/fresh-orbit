using AwesomeAssertions;
using FreshOrbit.Identity.Application.Auth;
using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.Identity.Application.Staff;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.Identity.Domain.Staff;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.Testing;
using NSubstitute;

namespace FreshOrbit.Identity.Tests.Unit;

/// <summary>D33 / IDN-R20 rules of the staff login, with the ports faked.</summary>
public sealed class StaffAuthServiceTests
{
    private const string Email = "packer@freshorbit.test";
    private const string RightPassword = "Right-Password-123";
    private const string UserHash = "user-hash";
    // The service keeps one dummy hash for the process, so tests only check that it is not the user's hash.
    private const string DummyHash = "dummy-hash";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IStaffRepository _staff = Substitute.For<IStaffRepository>();
    private readonly IStaffLoginFailureRepository _failures = Substitute.For<IStaffLoginFailureRepository>();
    private readonly IPasswordHashing _passwords = Substitute.For<IPasswordHashing>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRequestContext _request = Substitute.For<IRequestContext>();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero));
    private readonly IdentitySettings _settings = new();
    private readonly StaffAuthService _service;

    public StaffAuthServiceTests()
    {
        _passwords.Hash(Arg.Any<string>()).Returns(DummyHash);
        _passwords.Verify(UserHash, RightPassword, out _).Returns(true);
        _request.ClientIp.Returns("203.0.113.7");
        _failures.GetFailureTimesAsync(default!, default!, default, default).ReturnsForAnyArgs([]);
        var tokens = Substitute.For<IRefreshTokenRepository>();
        var roles = Substitute.For<IRoleRepository>();
        _service = new StaffAuthService(
            _users,
            _staff,
            roles,
            _failures,
            _passwords,
            new SessionIssuer(tokens, roles, Substitute.For<IAccessTokenIssuer>(), _clock, _settings),
            new SessionRevoker(tokens, _clock),
            _request,
            _unitOfWork,
            _clock,
            _settings);
    }

    private User GivenStaff(Action<User>? arrange = null, bool memberActive = true)
    {
        var user = User.CreateStaff(Email, UserHash);
        arrange?.Invoke(user);
        _users.FindStaffByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        var data = new StaffData("Packer", [Guid.CreateVersion7()], true, [], true);
        var member = StaffMember.Create(user.Id, Email, data);
        member.Update(data with { IsActive = memberActive });
        _staff.FindByUserAsync(user.Id, Arg.Any<CancellationToken>()).Returns(member);
        return user;
    }

    private void Lock(User user)
    {
        for (var i = 0; i < _settings.StaffMaxFailedLogins; i++)
        {
            user.RecordFailedPassword(_settings.StaffLockoutPolicy(), _clock.UtcNow);
        }
    }

    private Task<StaffLoginResponse> LoginAsync(string password = RightPassword)
        => _service.LoginAsync(new StaffLoginRequest(Email, password, null), CancellationToken.None);

    private static async Task ShouldRefuseAsync(Func<Task> login)
        => (await login.Should().ThrowAsync<UnauthorizedException>()).Which.Code.Should().Be(IdentityErrors.InvalidCredentials);

    [Fact]
    public async Task A_locked_account_with_the_right_password_gets_invalid_credentials_without_checking_its_hash()
    {
        GivenStaff(Lock);

        await ShouldRefuseAsync(() => LoginAsync());

        _passwords.DidNotReceive().Verify(UserHash, Arg.Any<string>(), out _);
        _passwords.Received(1).VerifyDummy(RightPassword);
        _failures.Received(1).Add(Arg.Is<StaffLoginFailure>(f => f.ClientIp == "203.0.113.7" && f.EmailHash == StaffLoginFailure.HashEmail(Email)));
    }

    [Fact]
    public async Task A_disabled_account_with_the_right_password_gets_invalid_credentials()
    {
        GivenStaff(u => u.Disable());

        await ShouldRefuseAsync(() => LoginAsync());

        _passwords.DidNotReceive().Verify(UserHash, Arg.Any<string>(), out _);
        _passwords.Received(1).VerifyDummy(RightPassword);
    }

    [Fact]
    public async Task Inactive_staff_with_the_right_password_gets_invalid_credentials()
    {
        GivenStaff(memberActive: false);

        await ShouldRefuseAsync(() => LoginAsync());

        _passwords.DidNotReceive().Verify(UserHash, Arg.Any<string>(), out _);
    }

    [Fact]
    public async Task The_locking_failure_answers_invalid_credentials_like_every_other_failure()
    {
        var user = GivenStaff();
        for (var i = 0; i < 10; i++)
        {
            await ShouldRefuseAsync(() => LoginAsync("Wrong-Password-000"));
        }

        user.Status.Should().Be(UserStatus.Locked);
        user.LockedUntil.Should().Be(_clock.UtcNow.AddMinutes(15));
    }

    [Fact]
    public async Task An_expired_lock_lets_the_password_be_checked_again()
    {
        var user = GivenStaff(Lock);
        _clock.Advance(TimeSpan.FromMinutes(15));

        await ShouldRefuseAsync(() => LoginAsync("Wrong-Password-000"));

        user.Status.Should().Be(UserStatus.Active);
        user.FailedLoginCount.Should().Be(1);
        _passwords.Received(1).Verify(UserHash, "Wrong-Password-000", out _);
    }

    [Fact]
    public async Task Five_failures_for_the_email_and_ip_give_429_before_any_password_check()
    {
        GivenStaff();
        var now = _clock.UtcNow;
        _failures.GetFailureTimesAsync(StaffLoginFailure.HashEmail(Email), "203.0.113.7", now.AddMinutes(-15), Arg.Any<CancellationToken>())
            .Returns([now.AddMinutes(-10), now.AddMinutes(-9), now.AddMinutes(-8), now.AddMinutes(-2), now.AddMinutes(-1)]);

        var refused = (await FluentActions.Awaiting(() => LoginAsync()).Should().ThrowAsync<TooManyRequestsException>()).Which;

        refused.Code.Should().Be(IdentityErrors.RateLimited);
        refused.RetryAfterSeconds.Should().Be(5 * 60, "the oldest counted failure leaves the 15-minute window in 5 minutes");
        _passwords.DidNotReceiveWithAnyArgs().Verify(default!, default!, out _);
        _failures.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Failures_before_the_last_sign_in_do_not_count()
    {
        var signedInAt = _clock.UtcNow.AddMinutes(-3);
        GivenStaff(u => u.RecordPasswordSignIn(signedInAt));

        await ShouldRefuseAsync(() => LoginAsync("Wrong-Password-000"));

        await _failures.Received(1).GetFailureTimesAsync(Arg.Any<string>(), Arg.Any<string>(), signedInAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_emails_are_counted_too_so_429_does_not_reveal_an_account()
    {
        await ShouldRefuseAsync(() => LoginAsync());

        _failures.Received(1).Add(Arg.Any<StaffLoginFailure>());
        _passwords.Received(1).VerifyDummy(RightPassword);
    }
}
