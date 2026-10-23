using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Customers.Application.Shared;

/// <summary>Loads the calling customer (CUS-R1: a DELETED customer is refused with CUSTOMER_DELETED).</summary>
public sealed class CustomerContext(ICurrentActor actor, ICustomersRepository repository, IClock clock)
{
    public DateTimeOffset Now => clock.UtcNow;

    public Guid CustomerId => actor.CustomerId ?? throw new NotFoundException("The caller is not a customer.");

    public async Task<Customer> LoadAsync(CancellationToken cancellationToken)
    {
        var customer = await repository.GetAsync(CustomerId, cancellationToken)
            ?? throw new NotFoundException("The customer does not exist.");
        customer.EnsureActive();
        return customer;
    }
}

internal static class Versions
{
    public static string Format(uint version) => version.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
