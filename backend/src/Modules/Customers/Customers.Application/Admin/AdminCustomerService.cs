using FluentValidation;
using FreshOrbit.Audit.Public;
using FreshOrbit.Customers.Application.Addresses;
using FreshOrbit.Customers.Application.Shared;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.Identity.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.Ordering.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Customers.Application.Admin;

/// <summary>
/// Query of <c>GET /admin/customers</c>: <c>q</c> matches an exact phone (E.164) or part of the name; <c>cityId</c>
/// keeps customers with an active saved address in a pincode served in that city (G6).
/// </summary>
public sealed record CustomerListRequest(string? Q = null, string? Status = null, int Page = 1, int PageSize = 20, Guid? CityId = null);

/// <summary>Query of <c>GET /admin/customers/{id}/orders</c>.</summary>
public sealed record CustomerOrdersRequest(int Page = 1, int PageSize = 20);

/// <summary>A customer's orders by outcome (G6); only orders at the caller's locations. PAYMENT_FAILED is not counted.</summary>
/// <param name="Total">Every order except PAYMENT_FAILED.</param>
/// <param name="Open">PENDING_PAYMENT, CONFIRMED, PREPARING, PACKED or OUT_FOR_DELIVERY.</param>
public sealed record CustomerOrderCountsResponse(int Total, int Open, int Delivered, int Cancelled, DateTimeOffset? LastPlacedAt)
{
    public static readonly CustomerOrderCountsResponse None = new(0, 0, 0, 0, null);
}

/// <summary>One order of a customer (G6), newest first. Money in paise: estimate until packed, then the final total.</summary>
public sealed record CustomerOrderListItem(
    Guid OrderId,
    string OrderNumber,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    Guid FulfillmentLocationId,
    DateTimeOffset PlacedAt,
    DateTimeOffset SlotStartAt,
    DateTimeOffset SlotEndAt,
    long TotalPaise,
    long? FinalTotalPaise);

public sealed record CustomerOrdersResponse(IReadOnlyList<CustomerOrderListItem> Items, int Page, int PageSize, int TotalCount);

public sealed record CustomerListItem(Guid CustomerId, string? FullName, string? Phone, string? Email, string Status, bool IsBlocked);

public sealed record CustomerListResponse(IReadOnlyList<CustomerListItem> Items, int Page, int PageSize, int TotalCount);

/// <summary>Lifetime value comes from Reporting and is not part of this response.</summary>
/// <param name="OrderCounts">From Ordering (G6), only orders at the caller's locations.</param>
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
    string Version,
    CustomerOrderCountsResponse OrderCounts);

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

public sealed class CustomerOrdersRequestValidator : AbstractValidator<CustomerOrdersRequest>
{
    public CustomerOrdersRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrorCode("PAGE_OUT_OF_RANGE");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithErrorCode("PAGE_SIZE_OUT_OF_RANGE");
    }
}

public interface IAdminCustomerService
{
    Task<CustomerListResponse> ListAsync(CustomerListRequest request, CancellationToken cancellationToken);

    Task<CustomerDetailResponse> GetAsync(Guid customerId, CancellationToken cancellationToken);

    Task<CustomerDetailResponse> SetBlockedAsync(Guid customerId, bool blocked, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>The customer's orders at the caller's locations (G6); 404 when the customer does not exist.</summary>
    Task<CustomerOrdersResponse> ListOrdersAsync(Guid customerId, CustomerOrdersRequest request, CancellationToken cancellationToken);
}

public sealed class AdminCustomerService(
    ICustomersRepository repository,
    IIdentityApi identity,
    IUnitOfWork unitOfWork,
    IAuditApi audit,
    IOrderingApi ordering,
    ILocationsApi locations,
    ICurrentActor actor) : IAdminCustomerService
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
        IReadOnlyCollection<string>? pincodes = request.CityId is { } cityId ? await locations.GetCityPincodesAsync(cityId, cancellationToken) : null;
        var (items, total) = await repository.SearchAsync(name, userIds, status, pincodes, (request.Page - 1) * request.PageSize, request.PageSize, cancellationToken);
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

    public async Task<CustomerOrdersResponse> ListOrdersAsync(Guid customerId, CustomerOrdersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = await LoadAsync(customerId, cancellationToken);
        var page = await ordering.ListCustomerOrdersAsync(customerId, VisibleLocations(), (request.Page - 1) * request.PageSize, request.PageSize, cancellationToken);
        return new CustomerOrdersResponse(
            [.. page.Items.Select(o => new CustomerOrderListItem(o.OrderId, o.OrderNumber, o.Status, o.PaymentMethod, o.FulfillmentLocationId, o.PlacedAt, o.SlotStartAt, o.SlotEndAt, o.TotalPaise, o.FinalTotalPaise))],
            request.Page,
            request.PageSize,
            page.TotalCount);
    }

    /// <summary>Null = every location (all-locations staff and system); otherwise the caller's locations.</summary>
    private IReadOnlyCollection<Guid>? VisibleLocations() => actor.IsAllLocations || actor.ActorType == ActorType.System ? null : actor.LocationIds;

    private async Task<Customer> LoadAsync(Guid customerId, CancellationToken cancellationToken)
        => await repository.GetAsync(customerId, cancellationToken) ?? throw new NotFoundException("The customer does not exist.");

    private async Task<CustomerDetailResponse> ToDetailAsync(Customer customer, CancellationToken cancellationToken)
    {
        var user = await identity.GetUserAsync(customer.UserId, cancellationToken);
        var addresses = await repository.GetActiveAddressesAsync(customer.Id, cancellationToken);
        var counts = await ordering.GetCustomerOrderCountsAsync([customer.Id], VisibleLocations(), cancellationToken);
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
            Versions.Format(customer.Version),
            counts.TryGetValue(customer.Id, out var c)
                ? new CustomerOrderCountsResponse(c.Total, c.Open, c.Delivered, c.Cancelled, c.LastPlacedAt)
                : CustomerOrderCountsResponse.None);
    }
}
