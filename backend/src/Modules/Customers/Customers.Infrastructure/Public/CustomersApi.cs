using FreshOrbit.Customers.Domain.Addresses;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.Customers.Public;
using FreshOrbit.Identity.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using DomainCustomerStatus = FreshOrbit.Customers.Domain.Customers.CustomerStatus;
using PublicCustomerStatus = FreshOrbit.Customers.Public.CustomerStatus;

namespace FreshOrbit.Customers.Infrastructure.Public;

internal sealed class CustomersApi(FreshOrbitDbContext db, IIdentityApi identity) : ICustomersApi
{
    public async Task<CustomerSummary?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Set<Customer>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var user = await identity.GetUserAsync(customer.UserId, cancellationToken);
        return new CustomerSummary(
            customer.Id,
            customer.UserId,
            customer.FullName ?? string.Empty,
            user?.Phone,
            customer.Email,
            customer.Status == DomainCustomerStatus.Deleted ? PublicCustomerStatus.Deleted : PublicCustomerStatus.Active);
    }

    public async Task<DeliveryAddress?> GetDeliveryAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken = default)
    {
        var address = await db.Set<Address>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId && a.IsActive, cancellationToken);
        return address is null
            ? null
            : new DeliveryAddress(
                address.Id,
                address.RecipientName,
                address.Phone,
                address.Line1,
                address.Line2,
                address.Landmark,
                address.Locality,
                address.CityName,
                address.StateCode,
                address.Pincode,
                address.Latitude,
                address.Longitude)
            {
                Label = address.Label.ToCode(),
            };
    }

    public Task<Guid?> FindCustomerIdByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => db.Set<Customer>().AsNoTracking().Where(c => c.UserId == userId).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);
}

/// <summary>Identity's view of customers (ICustomerAccountDirectory): creation at first login, id and e-mail lookups.</summary>
internal sealed class CustomerAccountDirectory(FreshOrbitDbContext db) : ICustomerAccountDirectory
{
    public async Task<CustomerAccount> EnsureCustomerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var customer = db.Set<Customer>().Local.FirstOrDefault(c => c.UserId == userId)
            ?? await db.Set<Customer>().FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (customer is not null)
        {
            return new CustomerAccount(customer.Id, false, customer.FullName, customer.Status == DomainCustomerStatus.Deleted);
        }

        customer = Customer.Register(userId);
        db.Add(customer);
        return new CustomerAccount(customer.Id, true, null, false);
    }

    public Task<Guid?> FindCustomerIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => db.Set<Customer>().AsNoTracking().Where(c => c.UserId == userId).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);

    public Task<Guid?> FindUserIdAsync(Guid customerId, CancellationToken cancellationToken = default)
        => db.Set<Customer>().AsNoTracking().Where(c => c.Id == customerId).Select(c => (Guid?)c.UserId).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> FindEmailsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await db.Set<Customer>().AsNoTracking()
            .Where(c => userIds.Contains(c.UserId) && c.Email != null)
            .Select(c => new { c.UserId, c.Email })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.UserId, r => r.Email!);
    }
}
