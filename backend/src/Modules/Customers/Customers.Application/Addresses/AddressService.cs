using FreshOrbit.Customers.Application.Shared;
using FreshOrbit.Customers.Domain.Addresses;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.Locations.Public;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Customers.Application.Addresses;

/// <summary>Body of POST and PUT <c>/me/addresses</c>. <c>label</c> is HOME / WORK / OTHER.</summary>
public sealed record SaveAddressRequest(
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
    bool? IsDefault);

/// <param name="IsServiceable">Whether Locations currently serves the pincode (CUS-R5); null where not resolved.</param>
public sealed record AddressResponse(
    Guid AddressId,
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
    bool IsDefault,
    bool? IsServiceable,
    string Version);

public interface IAddressService
{
    Task<IReadOnlyList<AddressResponse>> ListAsync(CancellationToken cancellationToken);

    Task<AddressResponse> AddAsync(SaveAddressRequest request, CancellationToken cancellationToken);

    Task<AddressResponse> ReplaceAsync(Guid addressId, SaveAddressRequest request, uint expectedVersion, CancellationToken cancellationToken);

    Task DeleteAsync(Guid addressId, CancellationToken cancellationToken);

    Task<AddressResponse> MakeDefaultAsync(Guid addressId, CancellationToken cancellationToken);
}

/// <summary>CUS-R3–R5. Every change stamps the customer row so concurrent address edits of one customer race there.</summary>
public sealed class AddressService(CustomerContext context, ICustomersRepository repository, IUnitOfWork unitOfWork, ILocationsApi locations) : IAddressService
{
    public async Task<IReadOnlyList<AddressResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var customer = await context.LoadAsync(cancellationToken);
        var addresses = await repository.GetActiveAddressesAsync(customer.Id, cancellationToken);
        var serviceable = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var pincode in addresses.Select(a => a.Pincode).Distinct(StringComparer.Ordinal))
        {
            serviceable[pincode] = (await locations.ResolvePincodeAsync(pincode, cancellationToken)).IsServiceable;
        }

        return [.. addresses.Select(a => ToResponse(a, serviceable[a.Pincode]))];
    }

    public async Task<AddressResponse> AddAsync(SaveAddressRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var data = ToData(request);
        var customer = await context.LoadAsync(cancellationToken);
        var active = await repository.GetActiveAddressesAsync(customer.Id, cancellationToken);
        if (active.Count >= CustomersErrors.MaxActiveAddresses)
        {
            throw new DomainException(CustomersErrors.AddressLimitReached, $"A customer can keep at most {CustomersErrors.MaxActiveAddresses} addresses.");
        }

        var address = Address.Create(customer.Id, data, context.Now);
        var currentDefault = active.FirstOrDefault(a => a.IsDefault);
        var makeDefault = currentDefault is null || request.IsDefault == true;
        var added = await repository.InTransactionAsync(
            async () =>
            {
                customer.TouchAddresses(context.Now);
                if (makeDefault && currentDefault is not null)
                {
                    currentDefault.ClearDefault();
                    await repository.FlushAsync(cancellationToken);
                }

                if (makeDefault)
                {
                    address.MakeDefault();
                }

                repository.Add(address);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return address;
            },
            cancellationToken);
        return await RespondAsync(added, cancellationToken);
    }

    public async Task<AddressResponse> ReplaceAsync(Guid addressId, SaveAddressRequest request, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var data = ToData(request);
        var customer = await context.LoadAsync(cancellationToken);
        var address = await LoadActiveAsync(customer.Id, addressId, cancellationToken);
        unitOfWork.SetExpectedVersion(address, expectedVersion);
        address.Replace(data);
        if (request.IsDefault == true && !address.IsDefault)
        {
            return await MoveDefaultAsync(customer, address, cancellationToken);
        }

        customer.TouchAddresses(context.Now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await RespondAsync(address, cancellationToken);
    }

    public async Task DeleteAsync(Guid addressId, CancellationToken cancellationToken)
    {
        var customer = await context.LoadAsync(cancellationToken);
        var address = await repository.GetAddressAsync(customer.Id, addressId, cancellationToken)
            ?? throw new NotFoundException("The address does not exist.");
        if (!address.IsActive)
        {
            return;
        }

        var wasDefault = address.IsDefault;
        await repository.InTransactionAsync(
            async () =>
            {
                customer.TouchAddresses(context.Now);
                address.Deactivate();
                if (wasDefault)
                {
                    await repository.FlushAsync(cancellationToken);
                    var remaining = await repository.GetActiveAddressesAsync(customer.Id, cancellationToken);
                    var newest = remaining.Where(a => a.Id != address.Id).OrderByDescending(a => a.AddedAt).ThenByDescending(a => a.Id).FirstOrDefault();
                    newest?.MakeDefault();
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            },
            cancellationToken);
    }

    public async Task<AddressResponse> MakeDefaultAsync(Guid addressId, CancellationToken cancellationToken)
    {
        var customer = await context.LoadAsync(cancellationToken);
        var address = await LoadActiveAsync(customer.Id, addressId, cancellationToken);
        if (address.IsDefault)
        {
            return await RespondAsync(address, cancellationToken);
        }

        return await MoveDefaultAsync(customer, address, cancellationToken);
    }

    private async Task<AddressResponse> MoveDefaultAsync(Domain.Customers.Customer customer, Address address, CancellationToken cancellationToken)
    {
        await repository.InTransactionAsync(
            async () =>
            {
                customer.TouchAddresses(context.Now);
                var active = await repository.GetActiveAddressesAsync(customer.Id, cancellationToken);
                foreach (var other in active.Where(a => a.IsDefault && a.Id != address.Id))
                {
                    other.ClearDefault();
                }

                await repository.FlushAsync(cancellationToken);
                address.MakeDefault();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            },
            cancellationToken);
        return await RespondAsync(address, cancellationToken);
    }

    private async Task<AddressResponse> RespondAsync(Address address, CancellationToken cancellationToken)
        => ToResponse(address, (await locations.ResolvePincodeAsync(address.Pincode, cancellationToken)).IsServiceable);

    private async Task<Address> LoadActiveAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken)
    {
        var address = await repository.GetAddressAsync(customerId, addressId, cancellationToken);
        return address is { IsActive: true } ? address : throw new NotFoundException("The address does not exist.");
    }

    private static AddressData ToData(SaveAddressRequest request)
    {
        if (!EnumCodes.TryToEnum<AddressLabel>(request.Label, out var label))
        {
            throw new RefusalException(new FieldError("label", "VALUE_INVALID", "The label must be HOME, WORK or OTHER."));
        }

        var data = new AddressData(
            label,
            request.RecipientName,
            request.Phone,
            request.Line1,
            request.Line2,
            request.Landmark,
            request.Locality,
            request.CityName,
            request.StateCode,
            request.Pincode,
            request.Latitude,
            request.Longitude);
        var errors = Address.Validate(data);
        return errors.Count > 0 ? throw new RefusalException(errors) : data;
    }

    public static AddressResponse ToResponse(Address address, bool? isServiceable = null)
    {
        ArgumentNullException.ThrowIfNull(address);
        return new AddressResponse(
            address.Id,
            EnumCodes.ToCode(address.Label),
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
            address.Longitude,
            address.IsDefault,
            isServiceable,
            Versions.Format(address.Version));
    }
}
