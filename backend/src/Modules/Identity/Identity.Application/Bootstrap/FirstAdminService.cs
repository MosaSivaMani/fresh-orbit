using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.Identity.Application.Staff;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.Identity.Domain.Staff;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Identity.Application.Bootstrap;

public sealed record CreateFirstAdminCommand(string Email, string FullName, string Password);

/// <summary>
/// D18 / IDN-R19: the one-time <c>--create-admin</c> command of the Migrations host. Run after migrations and
/// reference data (the ADMIN role must exist).
/// </summary>
public interface IFirstAdminService
{
    /// <summary>Creates the first ADMIN staff member (all locations) and returns its staff member id.</summary>
    /// <exception cref="DomainException">
    /// <c>ADMIN_ALREADY_EXISTS</c>, <c>DUPLICATE</c> (email), <c>PASSWORD_TOO_WEAK</c>, <c>CONFIGURATION_MISSING</c> (no ADMIN role),
    /// <c>VALIDATION_FAILED</c> (email or name).
    /// </exception>
    Task<Guid> CreateFirstAdminAsync(CreateFirstAdminCommand command, CancellationToken cancellationToken);
}

public sealed class FirstAdminService(IUserRepository users, IStaffRepository staff, IRoleRepository roles, IPasswordHashing passwords, IUnitOfWork unitOfWork) : IFirstAdminService
{
    public async Task<Guid> CreateFirstAdminAsync(CreateFirstAdminCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var validation = new CreateStaffRequestValidator().Validate(new CreateStaffRequest(command.Email ?? string.Empty, command.FullName ?? string.Empty, [Guid.Empty], true, null, command.Password ?? string.Empty));
        if (!validation.IsValid)
        {
            throw new RefusalException([.. validation.Errors.Select(e => new FieldError(char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..], "VALIDATION_FAILED", e.ErrorMessage))]);
        }

        var admin = await roles.FindByCodeAsync(IdentityErrors.AdminRoleCode, cancellationToken)
            ?? throw new DomainException(IdentityErrors.ConfigurationMissing, "The ADMIN role is missing; load reference data before creating the first admin.");
        if (await staff.CountActiveWithRoleAsync(admin.Id, null, cancellationToken) > 0)
        {
            throw new DomainException(IdentityErrors.AdminAlreadyExists, "An active administrator already exists; use the admin dashboard to add staff.");
        }

        var email = EmailAddresses.Normalize(command.Email!);
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException(IdentityErrors.Duplicate, "A user with this email already exists.", "email");
        }

        PasswordPolicy.Ensure(command.Password, "password");
        var user = User.CreateStaff(email, passwords.Hash(command.Password!));
        var member = StaffMember.Create(user.Id, email, new StaffData(command.FullName!, [admin.Id], true, [], true));
        users.Add(user);
        staff.Add(member);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return member.Id;
    }
}
