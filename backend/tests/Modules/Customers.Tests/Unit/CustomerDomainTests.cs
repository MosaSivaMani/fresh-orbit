using AwesomeAssertions;
using FreshOrbit.Customers.Domain.Addresses;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.Customers.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Customers.Tests.Unit;

public sealed class CustomerTests
{
    [Fact]
    public void Profile_update_changes_only_the_fields_sent()
    {
        var customer = Customer.Register(Guid.CreateVersion7());
        customer.UpdateProfile("Asha", "asha@example.in", true);
        customer.UpdateProfile(null, null, false);

        customer.FullName.Should().Be("Asha");
        customer.Email.Should().Be("asha@example.in");
        customer.MarketingOptIn.Should().BeFalse();
    }

    [Fact]
    public void Delete_removes_personal_data_and_is_terminal()
    {
        var customer = Customer.Register(Guid.CreateVersion7());
        customer.UpdateProfile("Asha", "asha@example.in", true);

        customer.Delete(DateTimeOffset.UtcNow);

        customer.Status.Should().Be(CustomerStatus.Deleted);
        customer.FullName.Should().BeNull();
        customer.Email.Should().BeNull();
        customer.MarketingOptIn.Should().BeFalse();
        customer.Invoking(c => c.UpdateProfile("X", null, null)).Should().Throw<DomainException>().Which.Code.Should().Be(CustomersErrors.CustomerDeleted);
        customer.Invoking(c => c.Unblock()).Should().Throw<ConflictException>();
    }

    [Fact]
    public void Block_is_idempotent_and_raises_one_event()
    {
        var customer = Customer.Register(Guid.CreateVersion7());
        customer.ClearEvents();
        customer.Block();
        customer.Block();

        customer.IsBlocked.Should().BeTrue();
        customer.DomainEvents.Should().ContainSingle();
    }
}

public sealed class AddressTests
{
    public static AddressData Valid() => new(AddressLabel.Home, "Asha", "+919876543210", "12 MG Road", null, null, "Anna Nagar", "Chennai", "tn", "600040", 13.08m, 80.21m);

    [Fact]
    public void Valid_data_is_normalised()
    {
        var address = Address.Create(Guid.CreateVersion7(), Valid(), DateTimeOffset.UtcNow);

        address.StateCode.Should().Be("TN");
        address.IsActive.Should().BeTrue();
        address.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Invalid_fields_are_all_reported()
    {
        var data = Valid() with { Phone = "98765", Pincode = "06004", StateCode = "Tamil", Latitude = 95m, Longitude = null, RecipientName = " " };

        Address.Validate(data).Select(e => e.Field).Should().BeEquivalentTo(["recipientName", "phone", "stateCode", "pincode", "latitude", "latitude"]);
    }

    [Fact]
    public void Anonymise_replaces_personal_fields_and_deactivates()
    {
        var address = Address.Create(Guid.CreateVersion7(), Valid(), DateTimeOffset.UtcNow);
        address.MakeDefault();

        address.Anonymise();

        address.IsActive.Should().BeFalse();
        address.IsDefault.Should().BeFalse();
        address.RecipientName.Should().Be("Deleted");
        address.Latitude.Should().BeNull();
        address.Invoking(a => a.Replace(Valid())).Should().Throw<ConflictException>();
    }
}
