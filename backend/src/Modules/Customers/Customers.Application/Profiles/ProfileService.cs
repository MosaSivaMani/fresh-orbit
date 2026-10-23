using FluentValidation;
using FreshOrbit.Customers.Application.Shared;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.Customers.Public;
using FreshOrbit.Identity.Public;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Customers.Application.Profiles;

/// <summary><c>PATCH /me</c>: null leaves a field unchanged (CUS-R2).</summary>
public sealed record UpdateProfileRequest(string? FullName, string? Email, bool? MarketingOptIn);

public sealed record ProfileResponse(
    Guid CustomerId,
    string? Phone,
    string? FullName,
    string? Email,
    bool MarketingOptIn,
    string Status,
    string Version);

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName!).Must(n => n.Trim().Length is >= 1 and <= 100).When(x => x.FullName is not null)
            .WithErrorCode("VALUE_INVALID").WithMessage("The fullName must be 1 to 100 characters.");
        RuleFor(x => x.Email!).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithErrorCode("EMAIL_INVALID").WithMessage("The email must be a valid e-mail address of at most 254 characters.");
    }
}

public interface IProfileService
{
    Task<ProfileResponse> GetAsync(CancellationToken cancellationToken);

    Task<ProfileResponse> UpdateAsync(UpdateProfileRequest request, uint expectedVersion, CancellationToken cancellationToken);

    Task DeleteAccountAsync(uint expectedVersion, CancellationToken cancellationToken);
}

public sealed class ProfileService(
    CustomerContext context,
    ICustomersRepository repository,
    IIdentityApi identity,
    IEnumerable<ICustomerDeletionCheck> deletionChecks,
    IUnitOfWork unitOfWork) : IProfileService
{
    public async Task<ProfileResponse> GetAsync(CancellationToken cancellationToken)
    {
        var customer = await context.LoadAsync(cancellationToken);
        return await ToResponseAsync(customer, cancellationToken);
    }

    public async Task<ProfileResponse> UpdateAsync(UpdateProfileRequest request, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var customer = await context.LoadAsync(cancellationToken);
        unitOfWork.SetExpectedVersion(customer, expectedVersion);
        customer.UpdateProfile(request.FullName, request.Email, request.MarketingOptIn);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(customer, cancellationToken);
    }

    /// <summary>CUS-R7.</summary>
    public async Task DeleteAccountAsync(uint expectedVersion, CancellationToken cancellationToken)
    {
        var customer = await context.LoadAsync(cancellationToken);
        unitOfWork.SetExpectedVersion(customer, expectedVersion);
        var reasons = new List<string>();
        foreach (var check in deletionChecks)
        {
            reasons.AddRange(await check.GetBlockingItemsAsync(customer.Id, cancellationToken));
        }

        if (reasons.Count > 0)
        {
            throw new ConflictException(
                CustomersErrors.InvalidStatusTransition,
                $"The account cannot be deleted yet: {string.Join(", ", reasons.Distinct(StringComparer.Ordinal))}.");
        }

        customer.Delete(context.Now);
        customer.TouchAddresses(context.Now);
        foreach (var address in await repository.GetActiveAddressesAsync(customer.Id, cancellationToken))
        {
            address.Anonymise();
        }

        foreach (var item in await repository.GetWishlistAsync(customer.Id, cancellationToken))
        {
            repository.Remove(item);
        }

        await identity.AnonymiseUserAsync(customer.UserId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<ProfileResponse> ToResponseAsync(Customer customer, CancellationToken cancellationToken)
    {
        var user = await identity.GetUserAsync(customer.UserId, cancellationToken);
        return new ProfileResponse(
            customer.Id,
            user?.Phone,
            customer.FullName,
            customer.Email,
            customer.MarketingOptIn,
            customer.Status == Domain.Customers.CustomerStatus.Deleted ? "DELETED" : "ACTIVE",
            Versions.Format(customer.Version));
    }
}
