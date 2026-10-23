using FluentValidation;
using FreshOrbit.Identity.Application.Auth;

namespace FreshOrbit.Identity.Application.Staff;

public sealed record StaffLoginRequest(string Email, string Password, string? DeviceLabel);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record CreateStaffRequest(string Email, string FullName, IReadOnlyList<Guid> RoleIds, bool AllLocations, IReadOnlyList<Guid>? LocationIds, string TemporaryPassword);

public sealed record UpdateStaffRequest(string FullName, IReadOnlyList<Guid> RoleIds, bool AllLocations, IReadOnlyList<Guid>? LocationIds, bool IsActive);

public sealed record ResetStaffPasswordRequest(string TemporaryPassword);

public sealed record StaffListRequest(string? Q, bool? IsActive, int Page = 1, int PageSize = 20);

public sealed record StaffRoleRef(Guid Id, string Code, string Name);

public sealed record StaffResponse(
    Guid Id,
    Guid UserId,
    string? Email,
    string FullName,
    IReadOnlyList<StaffRoleRef> Roles,
    bool AllLocations,
    IReadOnlyList<Guid> LocationIds,
    bool IsActive,
    string UserStatus,
    DateTimeOffset? LastLoginAt,
    string Version);

public sealed record StaffListResponse(IReadOnlyList<StaffResponse> Items, int Page, int PageSize, int TotalCount);

public sealed record StaffProfileResponse(
    Guid StaffId,
    Guid UserId,
    string? Email,
    string FullName,
    IReadOnlyList<StaffRoleRef> Roles,
    IReadOnlyList<string> Permissions,
    bool AllLocations,
    IReadOnlyList<Guid> LocationIds);

public sealed record StaffLoginResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    StaffProfileResponse Staff)
{
    public static StaffLoginResponse From(SessionTokensResponse session, StaffProfileResponse staff)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new StaffLoginResponse(session.AccessToken, session.AccessTokenExpiresAt, session.RefreshToken, session.RefreshTokenExpiresAt, staff);
    }
}

/// <summary>Read side for staff lists and details.</summary>
public interface IStaffReads
{
    Task<StaffListResponse> ListAsync(StaffListRequest request, CancellationToken cancellationToken);

    Task<StaffResponse?> GetAsync(Guid staffMemberId, CancellationToken cancellationToken);
}

public sealed class StaffLoginRequestValidator : AbstractValidator<StaffLoginRequest>
{
    public StaffLoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DeviceLabel).MaximumLength(100);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateStaffRequestValidator : AbstractValidator<CreateStaffRequest>
{
    public CreateStaffRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RoleIds).NotEmpty().WithErrorCode("ROLE_REQUIRED");
        RuleFor(x => x.TemporaryPassword).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateStaffRequestValidator : AbstractValidator<UpdateStaffRequest>
{
    public UpdateStaffRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RoleIds).NotEmpty().WithErrorCode("ROLE_REQUIRED");
    }
}

public sealed class StaffListRequestValidator : AbstractValidator<StaffListRequest>
{
    public StaffListRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrorCode("PAGE_OUT_OF_RANGE");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithErrorCode("PAGE_SIZE_OUT_OF_RANGE");
    }
}

internal static class EmailAddresses
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
