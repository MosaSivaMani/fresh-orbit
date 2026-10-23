using FluentValidation;
using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.Identity.Domain.Tokens;
using FreshOrbit.Identity.Public;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;
using DomainUserType = FreshOrbit.Identity.Domain.Users.UserType;
using DomainUserStatus = FreshOrbit.Identity.Domain.Users.UserStatus;

namespace FreshOrbit.Identity.Application.Auth;

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string? RefreshToken, bool AllDevices = false);

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(200);
}

public interface ITokenService
{
    Task<SessionTokensResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);

    Task LogoutAsync(Guid userId, LogoutRequest request, CancellationToken cancellationToken);
}

/// <summary>Refresh rotation with family revocation (IDN-R8–R10).</summary>
public sealed class TokenService(
    IRefreshTokenRepository tokens,
    IUserRepository users,
    IStaffRepository staff,
    ICustomerAccountDirectory customers,
    SessionIssuer sessions,
    SessionRevoker revoker,
    IUnitOfWork unitOfWork,
    IClock clock) : ITokenService
{
    public async Task<SessionTokensResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.UtcNow;
        var token = await tokens.FindByHashAsync(OpaqueTokens.Hash(request.RefreshToken), cancellationToken) ?? throw Invalid();
        if (token.WasRotated)
        {
            foreach (var member in await tokens.GetActiveFamilyAsync(token.FamilyId, now, cancellationToken))
            {
                member.Revoke(now, RevokeReason.FamilyReuse);
            }

            token.ReportReuse(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException(IdentityErrors.RefreshTokenReused, "This refresh token was already used; sign in again.");
        }

        if (!token.IsActive(now))
        {
            throw Invalid();
        }

        var user = await users.GetAsync(token.UserId, cancellationToken);
        if (user is null || user.Status != DomainUserStatus.Active)
        {
            throw Invalid();
        }

        SessionTokensResponse session;
        if (user.UserType == DomainUserType.Customer)
        {
            var customerId = await customers.FindCustomerIdAsync(user.Id, cancellationToken) ?? throw Invalid();
            session = sessions.IssueCustomer(user, customerId, token, null);
        }
        else
        {
            var member = await staff.FindByUserAsync(user.Id, cancellationToken);
            if (member is null || !member.IsActive)
            {
                throw Invalid();
            }

            session = await sessions.IssueStaffAsync(user, member, token, null, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task LogoutAsync(Guid userId, LogoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.AllDevices)
        {
            await revoker.RevokeAllAsync(userId, RevokeReason.Logout, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            var token = await tokens.FindByHashAsync(OpaqueTokens.Hash(request.RefreshToken), cancellationToken);
            if (token is not null && token.UserId == userId)
            {
                token.Revoke(clock.UtcNow, RevokeReason.Logout);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static UnauthorizedException Invalid() => new(IdentityErrors.RefreshTokenInvalid, "The refresh token is not valid; sign in again.");
}
