using AwesomeAssertions;
using FreshOrbit.Audit.Domain.Entries;
using FreshOrbit.Audit.Infrastructure.Recording;
using FreshOrbit.E1Testing;
using FreshOrbit.Identity.Domain.Roles;
using FreshOrbit.SharedKernel.Actors;

namespace FreshOrbit.Audit.Tests.Integration;

/// <summary>AUD-R9: which changed entities an explicit audit record covers (tracked only, nothing is saved).</summary>
public sealed class AuditChangeSetTests(E1Database database)
{
    private static readonly string[] NoPermissions = [];

    [Fact]
    public async Task Explicit_record_with_the_primary_key_covers_only_that_entity()
    {
        await using var db = database.CreateDbContext();
        var covered = NewRole();
        var other = NewRole();
        db.AddRange(covered, other);
        db.Add(Explicit("ROLE", covered.Id.ToString()));

        var changes = AuditChangeSet.Read(db, action: null);

        changes.Should().ContainSingle().Which.EntityId.Should().Be(other.Id.ToString());
        changes[0].EntityType.Should().Be("ROLE");
        changes[0].Action.Should().Be("ROLE_CREATED");
    }

    [Fact]
    public async Task Explicit_record_with_a_natural_key_and_a_qualified_type_covers_every_entity_of_that_class()
    {
        await using var db = database.CreateDbContext();
        db.AddRange(NewRole(), NewRole());
        db.Add(Explicit("IDENTITY_ROLE", "default"));

        AuditChangeSet.Read(db, action: null).Should().BeEmpty();
    }

    [Fact]
    public async Task Explicit_record_of_another_type_covers_nothing()
    {
        await using var db = database.CreateDbContext();
        var role = NewRole();
        db.Add(role);
        db.Add(Explicit("STAFF_MEMBER", role.Id.ToString()));

        AuditChangeSet.Read(db, action: "STAFF_UPDATED").Should().ContainSingle().Which.Action.Should().Be("STAFF_UPDATED");
    }

    private static Role NewRole() => Role.Create("AUDIT_SET_" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), "Test role", false, NoPermissions);

    private static AuditEntry Explicit(string entityType, string entityId)
        => AuditEntry.Create(new AuditEntryData(DateTimeOffset.UtcNow, ActorType.Staff, Guid.CreateVersion7(), null, "TEST_RECORDED", entityType, entityId, null, null, null, null));
}
