using FluentValidation;
using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.Identity.Domain.Otp;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.Identity.Domain.Users;
using DomainUserStatus = FreshOrbit.Identity.Domain.Users.UserStatus;
using DomainUserType = FreshOrbit.Identity.Domain.Users.UserType;
using FreshOrbit.Identity.Public;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;
using DomainOtpPurpose = FreshOrbit.Identity.Domain.Otp.OtpPurpose;
using PublicOtpPurpose = FreshOrbit.Identity.Public.OtpPurpose;

namespace FreshOrbit.Identity.Application.Auth;

public sealed record OtpRequestRequest(string Phone, DomainOtpPurpose Purpose = DomainOtpPurpose.Login);

public sealed record OtpRequestResponse(Guid ChallengeId, DateTimeOffset ExpiresAt, int ResendAfterSeconds, int CodeLength);

public sealed record OtpVerifyRequest(Guid ChallengeId, string Phone, string Code, string? DeviceLabel, string? GuestCartToken);

public sealed record CustomerLoginUser(Guid Id, string? Phone, string? FullName, bool IsNewCustomer);

/// <summary>
/// The customer's cart after the guest cart named by <c>guestCartToken</c> was merged into it (CART-R27..R30); null
/// when no token was sent or it names no ACTIVE guest cart. Read the priced cart with <c>GET /cart</c>.
/// </summary>
/// <param name="DroppedLineCount">Guest lines that could not be kept (variant gone, or the cart line limit reached).</param>
public sealed record CustomerLoginCart(Guid Id, int MergedLineCount, int DroppedLineCount);

public sealed record CustomerLoginResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    CustomerLoginUser User,
    CustomerLoginCart? Cart);

public sealed class OtpRequestRequestValidator : AbstractValidator<OtpRequestRequest>
{
    public OtpRequestRequestValidator()
    {
        RuleFor(x => x.Phone).Must(PhoneNumbers.IsE164).WithErrorCode("PHONE_INVALID").WithMessage("The phone must be an E.164 number such as +919876543210.");
        RuleFor(x => x.Purpose).Equal(DomainOtpPurpose.Login).WithErrorCode("PURPOSE_NOT_SUPPORTED").WithMessage("The purpose must be LOGIN.");
    }
}

public sealed class OtpVerifyRequestValidator : AbstractValidator<OtpVerifyRequest>
{
    public OtpVerifyRequestValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.Phone).Must(PhoneNumbers.IsE164).WithErrorCode("PHONE_INVALID").WithMessage("The phone must be an E.164 number such as +919876543210.");
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{4,8}$").WithErrorCode("OTP_FORMAT_INVALID").WithMessage("The code must be the digits from the SMS.");
        RuleFor(x => x.DeviceLabel).MaximumLength(100);
        RuleFor(x => x.GuestCartToken).MaximumLength(200);
    }
}

public interface IOtpService
{
    Task<OtpRequestResponse> RequestAsync(OtpRequestRequest request, CancellationToken cancellationToken);

    Task<CustomerLoginResponse> VerifyAsync(OtpVerifyRequest request, CancellationToken cancellationToken);
}

/// <summary>Customer phone + OTP login (IDN-R1–R8).</summary>
public sealed class OtpService(
    IOtpChallengeRepository challenges,
    IUserRepository users,
    ICustomerAccountDirectory customers,
    IOtpSender sender,
    IOtpCodeHasher hasher,
    SessionIssuer sessions,
    IRequestContext requestContext,
    IUnitOfWork unitOfWork,
    IClock clock,
    IdentitySettings settings,
    IEnumerable<IGuestCartMerger> cartMergers) : IOtpService
{
    public async Task<OtpRequestResponse> RequestAsync(OtpRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.UtcNow;
        var ip = requestContext.ClientIp;
        await EnforceLimitsAsync(request.Phone, ip, now, cancellationToken);

        var code = OpaqueTokens.NewNumericCode(IdentitySettings.OtpCodeLength);
        var id = IdGenerator.NewId();
        var lifetime = TimeSpan.FromMinutes(settings.OtpLifetimeMinutes);
        var challenge = OtpChallenge.Create(id, request.Phone, request.Purpose, hasher.Hash(id, code), now, lifetime, ip);
        challenges.Add(challenge);
        var user = await users.FindByPhoneAsync(request.Phone, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // IDN-R2: a disabled account gets the same answer but no SMS.
        if (user is null || user.Status == DomainUserStatus.Active)
        {
            await sender.SendAsync(new OtpMessage(request.Phone, PublicOtpPurpose.Login, code, settings.OtpLifetimeMinutes), cancellationToken);
        }

        return new OtpRequestResponse(challenge.Id, challenge.ExpiresAt, settings.OtpResendSeconds, IdentitySettings.OtpCodeLength);
    }

    public async Task<CustomerLoginResponse> VerifyAsync(OtpVerifyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.UtcNow;
        var challenge = await challenges.GetAsync(request.ChallengeId, cancellationToken);
        if (challenge is null || !string.Equals(challenge.Phone, request.Phone, StringComparison.Ordinal))
        {
            throw new DomainException(IdentityErrors.OtpInvalid, "The code is not valid for this phone; check the code or request a new one.", "code");
        }

        var check = challenge.Verify(hasher.Hash(challenge.Id, request.Code), now, settings.OtpMaxAttempts);
        if (check != OtpCheck.Accepted)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw check switch
            {
                OtpCheck.Expired => new DomainException(IdentityErrors.OtpExpired, "The code has expired; request a new one.", "code"),
                OtpCheck.TooManyAttempts => new TooManyRequestsException(IdentityErrors.OtpTooManyAttempts, "Too many wrong codes; request a new one.", settings.OtpResendSeconds),
                _ => new DomainException(IdentityErrors.OtpInvalid, "The code is not valid for this phone; check the code or request a new one.", "code"),
            };
        }

        var user = await users.FindByPhoneAsync(request.Phone, cancellationToken);
        if (user is null)
        {
            user = User.RegisterCustomer(request.Phone, now);
            users.Add(user);
        }
        else if (user.Status != DomainUserStatus.Active || user.UserType != DomainUserType.Customer)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ForbiddenException(IdentityErrors.AccountDisabled, "This account is disabled.");
        }

        var account = await customers.EnsureCustomerAsync(user.Id, cancellationToken);
        if (account.IsDeleted)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new ForbiddenException(IdentityErrors.AccountDisabled, "This account was deleted.");
        }

        user.RecordCustomerSignIn(account.CustomerId, account.IsNew, request.GuestCartToken, request.DeviceLabel, now);
        var session = sessions.IssueCustomer(user, account.CustomerId, null, request.DeviceLabel);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Follow-on after the login committed: Cart merges in its own transaction so the answer carries the cart; the
        // CustomerSignedIn handler then finds the guest cart merged and does nothing.
        var cart = await MergeGuestCartAsync(request.GuestCartToken, account.CustomerId, cancellationToken);
        return new CustomerLoginResponse(
            session.AccessToken,
            session.AccessTokenExpiresAt,
            session.RefreshToken,
            session.RefreshTokenExpiresAt,
            new CustomerLoginUser(user.Id, user.Phone, account.FullName, account.IsNew),
            cart);
    }

    private async Task<CustomerLoginCart?> MergeGuestCartAsync(string? guestCartToken, Guid customerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(guestCartToken) || cartMergers.FirstOrDefault() is not { } merger)
        {
            return null;
        }

        try
        {
            var merged = await merger.MergeAsync(guestCartToken, customerId, cancellationToken);
            return merged is null ? null : new CustomerLoginCart(merged.CartId, merged.MergedLineCount, merged.DroppedLineCount);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // CART-R27: a failed merge never fails the login (it has committed); the CustomerSignedIn handler retries it.
            return null;
        }
    }

    /// <summary>IDN-R3, IDN-R4.</summary>
    private async Task EnforceLimitsAsync(string phone, string? ip, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var recent = await challenges.GetRequestTimesForPhoneAsync(phone, now.AddDays(-1), cancellationToken);
        if (recent.Count > 0)
        {
            var sinceLast = now - recent[0];
            if (sinceLast < TimeSpan.FromSeconds(settings.OtpResendSeconds))
            {
                throw new TooManyRequestsException(IdentityErrors.OtpResendTooSoon, "A code was sent moments ago; wait before asking for another.", (int)Math.Ceiling(settings.OtpResendSeconds - sinceLast.TotalSeconds));
            }
        }

        var window = now.AddMinutes(-10);
        var lastTenMinutes = recent.Where(t => t > window).ToList();
        if (lastTenMinutes.Count >= settings.OtpPerPhonePer10Minutes)
        {
            throw RateLimited(lastTenMinutes.Min().AddMinutes(10) - now);
        }

        if (recent.Count >= settings.OtpPerPhonePerDay)
        {
            throw RateLimited(recent.Min().AddDays(1) - now);
        }

        if (ip is not null)
        {
            var fromIp = await challenges.GetRequestTimesForIpAsync(ip, now.AddHours(-1), cancellationToken);
            if (fromIp.Count >= settings.OtpPerIpPerHour)
            {
                throw RateLimited(fromIp.Min().AddHours(1) - now);
            }
        }
    }

    private static TooManyRequestsException RateLimited(TimeSpan wait)
        => new(IdentityErrors.RateLimited, "Too many codes were requested; wait before trying again.", (int)Math.Ceiling(wait.TotalSeconds));
}
