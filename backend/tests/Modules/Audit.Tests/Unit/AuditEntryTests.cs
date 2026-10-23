using AwesomeAssertions;
using FreshOrbit.Audit.Domain.Entries;
using FreshOrbit.SharedKernel.Actors;

namespace FreshOrbit.Audit.Tests.Unit;

public sealed class AuditEntryTests
{
    private static AuditEntryData Data(string action, string entityId) => new(DateTimeOffset.UtcNow, ActorType.Staff, Guid.CreateVersion7(), "Asha", action, "price", entityId, null, null, "10.0.0.1", "corr");

    [Fact]
    public void Valid_entry_keeps_every_field()
    {
        var entry = AuditEntry.Create(Data("PRICE_CHANGED", "p-1"));

        entry.Action.Should().Be("PRICE_CHANGED");
        entry.ActorName.Should().Be("Asha");
        entry.Ip.Should().Be("10.0.0.1");
    }

    [Theory]
    [InlineData("priceChanged", "p-1")]
    [InlineData("PRICE_CHANGED", "")]
    public void Invalid_action_or_entity_is_refused(string action, string entityId)
        => FluentActions.Invoking(() => AuditEntry.Create(Data(action, entityId))).Should().Throw<ArgumentException>();
}
