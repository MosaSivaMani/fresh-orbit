using FluentValidation;
using FreshOrbit.Audit.Public;
using FreshOrbit.Customers.Application.Addresses;
using FreshOrbit.Customers.Application.Shared;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.Identity.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Customers.Application.Admin;

/// <summary>Query of <c>GET /admin/customers</c>: <c>q</c> matches an exact phone (E.164) or part of the name.</summary>
public sealed record CustomerListRequest(string? Q = null, string? Status = null, int Page = 1, int PageSize = 20);

public sealed record CustomerListItem(Guid CustomerId, string? FullName, string? Phone, string? Email, string Status, bool IsBlocked);

public sealed record CustomerListResponse(IReadOnlyList<CustomerListItem> Items, int Page, int PageSize, int TotalCount);

/// <summary>Order counts and lifetime value come from Ordering / Reporting and are not part of this response yet.</summary>
public sealed record CustomerDetailResponse(
    Guid CustomerId,
    Guid UserId,
    string? FullName,
    string? Phone,
    string? Email,
    bool MarketingOptIn,
    string Status,
    bool IsBlocked,
    DateTimeOffset? DeletedAt,
    IReadOnlyList<AddressResponse> Addresses,
    string Version);

public sealed class CustomerListRequestValidator : AbstractValidator<CustomerListRequest>
{
    public CustomerListRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrorCode("PAGE_OUT_OF_RANGE");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithErrorCode("PAGE_SIZE_OUT_OF_RANGE");
        RuleFor(x => x.Q).MaximumLength(100);
        RuleFor(x => x.Status).Must(s => s is null || EnumCodes.IsCode<CustomerStatus>(s)).WithErrorCode("VALUE_INVALID")
            .WithMessage("The status must be ACTIVE or DELETED.");
    }
}

public interface IAdminCustomerService
{
    Task<CustomerListResponse> ListAsync(CustomerListRequest request, CancellationToken cancellationToken);

    Task<CustomerDetailResponse> GetAsync(Guid customerId, CancellationToken cancellationToken);

    Task<CustomerDetailResponse> SetBlockedAsync(Guid customerId, bool blocked, uint expectedVersion, CancellationToken cancellationToken);
}

public sealed class AdminCustomerService(ICustomersRepository repository, IIdentityApi identity, IUnitOfWork unitOfWork, IAuditApi audit) : IAdminCustomerService
{
    public async Task<CustomerListResponse> ListAsync(CustomerListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? name = null;
        IReadOnlyCollection<Guid>? userIds = null;
        var q = request.Q?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            if (q.StartsWith('+') || q.All(char.IsDigit))
            {
                var phone = q.StartsWith('+') ? q : q.Length == 10 ? "+91" + q : "+" + q;
                var userId = await identity.FindUserIdByPhoneAsync(phone, cancellationToken);
                userIds = userId is { } id ? [id] : [];
            }
            else
            {
                name = q;
            }
        }

        CustomerStatus? status = request.Status is null ? null : request.Status.ToEnum<CustomerStatus>();
        var (items, total) = await repository.SearchAsync(name, userIds, status, (request.Page - 1) * request.PageSize, request.PageSize, cancellationToken);
        var users = await identity.GetUsersAsync([.. items.Select(c => c.UserId)], cancellationToken);
        var phones = users.ToDictionary(u => u.UserId, u => u.Phone);
        return new CustomerListResponse(
            [.. items.Select(c => new CustomerListItem(c.Id, c.FullName, phones.GetValueOrDefault(c.UserId), c.Email, c.Status.ToCode(), c.IsBlocked))],
            request.Page,
            request.PageSize,
            total);
    }

    public async Task<CustomerDetailResponse> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await LoadAsync(customerId, cancellationToken);
        return await ToDetailAsync(customer, cancellationToken);
    }

    /// <summary>CUS-R8.</summary>
    public async Task<CustomerDetailResponse> SetBlockedAsync(Guid customerId, bool blocked, uint expectedVersion, CancellationToken cancellationToken)
    {
        var customer = await LoadAsync(customerId, cancellationToken);
        unitOfWork.SetExpectedVersion(customer, expectedVersion);
        if (blocked)
        {
            customer.Block();
        }
        else
        {
            customer.Unblock();
        }

        if (customer.Status == CustomerStatus.Active)
        {
            await identity.SetUserBlockedAsync(customer.UserId, blocked, cancellationToken);
        }

        audit.Record(new AuditRecord(blocked ? "CUSTOMER_BLOCKED" : "CUSTOMER_UNBLOCKED", "CUSTOMER", customer.Id.ToString(), null, new { Status = EnumCodes.ToCode(customer.Status) }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDetailAsync(customer, cancellationToken);
    }

    private async Task<Customer> LoadAsync(Guid customerId, CancellationToken cancellationToken)
        => await repository.GetAsync(customerId, cancellationToken) ?? throw new NotFoundException("The customer does not exist.");

    private async Task<CustomerDetailResponse> ToDetailAsync(Customer customer, CancellationToken cancellationToken)
    {
        var user = await identity.GetUserAsync(customer.UserId, cancellationToken);
        var addresses = await repository.GetActiveAddressesAsync(customer.Id, cancellationToken);
        return new CustomerDetailResponse(
            customer.Id,
            customer.UserId,
            customer.FullName,
            user?.Phone,
            customer.Email,
            customer.MarketingOptIn,
            customer.Status.ToCode(),
            customer.IsBlocked,
            customer.DeletedAt,
            [.. addresses.Select(a => AddressService.ToResponse(a))],
            Versions.Format(customer.Version));
    }
}
