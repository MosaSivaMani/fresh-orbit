using FluentValidation;
using FreshOrbit.Customers.Application.Addresses;
using FreshOrbit.Customers.Application.Profiles;
using FreshOrbit.Customers.Application.Shared;
using FreshOrbit.Customers.Domain.Addresses;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.Customers.Public;
using FreshOrbit.Identity.Public;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Customers.Infrastructure.Seeding;

/// <summary>
/// Sample <c>customers.json</c> (order 70): profile fields and addresses through <see cref="ProfileService"/> and
/// <see cref="AddressService"/>, each run as that customer (the services act for the calling customer). The customer
/// rows exist already (Identity loader, first OTP verify path). A profile that has a name is left alone; an address
/// with the same first line and pincode is skipped.
/// </summary>
public sealed partial class CustomersSampleDataLoader(
    ICustomersRepository repository,
    IUnitOfWork unitOfWork,
    IIdentityApi identity,
    ILocationsApi locations,
    IEnumerable<ICustomerDeletionCheck> deletionChecks,
    IValidator<UpdateProfileRequest> profileValidator,
    IClock clock,
    FreshOrbitDbContext db,
    ILogger<CustomersSampleDataLoader> logger) : ISampleDataLoader
{
    private const string FileName = "customers.json";

    public int Order => 70;

    public async Task LoadAsync(SampleDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var file = await SampleFiles.ReadAsync<CustomersFile>(context, FileName, cancellationToken);
        var userIds = await identity.FindUserIdsByPhonesAsync([.. file.Customers.Select(c => c.Phone)], cancellationToken);
        var users = userIds.Values.ToList();
        var customers = await db.Set<Customer>().AsNoTracking().Where(c => users.Contains(c.UserId)).ToDictionaryAsync(c => c.UserId, cancellationToken);
        var customerIds = customers.Values.Select(c => c.Id).ToList();
        var addresses = (await db.Set<Address>().AsNoTracking().Where(a => customerIds.Contains(a.CustomerId) && a.IsActive).Select(a => new { a.CustomerId, a.Line1, a.Pincode }).ToListAsync(cancellationToken))
            .Select(a => (a.CustomerId, a.Line1, a.Pincode)).ToHashSet();

        var profiles = 0;
        var inserted = 0;
        foreach (var row in file.Customers)
        {
            var customer = userIds.TryGetValue(row.Phone, out var userId) && customers.TryGetValue(userId, out var c) ? c : throw SampleFiles.Missing(FileName, "customer user", row.Key);
            var acting = new CustomerContext(new SampleCustomerActor(userId, customer.Id), repository, clock);
            if (customer.FullName is null)
            {
                var request = new UpdateProfileRequest(row.FullName, row.Email, row.MarketingOptIn);
                await profileValidator.ValidateAndThrowAsync(request, cancellationToken);
                var current = await db.Set<Customer>().AsNoTracking().Where(x => x.Id == customer.Id).Select(x => x.Version).SingleAsync(cancellationToken);
                await new ProfileService(acting, repository, identity, deletionChecks, unitOfWork).UpdateAsync(request, current, cancellationToken);
                profiles++;
            }

            var service = new AddressService(acting, repository, unitOfWork, locations);

            // The default address first, so the others do not take the default (CUS-R4).
            foreach (var address in row.Addresses.OrderByDescending(a => a.IsDefault))
            {
                if (addresses.Contains((customer.Id, address.Line1, address.Pincode)))
                {
                    continue;
                }

                var saved = await service.AddAsync(
                    new SaveAddressRequest(address.Label, address.RecipientName, address.Phone, address.Line1, address.Line2, address.Landmark, address.Locality, address.CityName, address.StateCode, address.Pincode, address.Latitude, address.Longitude, address.IsDefault),
                    cancellationToken);
                context.Track<Address>(db, saved.AddressId);
                inserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        LogLoaded(profiles, inserted);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample customers: {Profiles} profiles filled, {Addresses} addresses inserted (existing skipped)")]
    private partial void LogLoaded(int profiles, int addresses);

    private sealed record CustomersFile(IReadOnlyList<CustomerRow> Customers);

    private sealed record CustomerRow(string Key, string Phone, string FullName, string? Email, bool MarketingOptIn, IReadOnlyList<AddressRow> Addresses);

    private sealed record AddressRow(
        string Label,
        string RecipientName,
        string Phone,
        string Line1,
        string? Line2,
        string? Landmark,
        string Locality,
        string CityName,
        string StateCode,
        string Pincode,
        decimal? Latitude,
        decimal? Longitude,
        bool IsDefault);

    /// <summary>The sample customer the profile and address services act for.</summary>
    private sealed class SampleCustomerActor(Guid userId, Guid customerId) : ICurrentActor
    {
        public ActorType ActorType => ActorType.Customer;

        public Guid? UserId => userId;

        public Guid? StaffMemberId => null;

        public Guid? CustomerId => customerId;

        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();

        public IReadOnlySet<Guid> LocationIds { get; } = new HashSet<Guid>();

        public bool IsAllLocations => false;
    }
}
