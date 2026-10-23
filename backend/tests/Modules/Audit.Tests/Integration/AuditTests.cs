using System.Net;
using AwesomeAssertions;
using FreshOrbit.Audit.Domain.Entries;
using FreshOrbit.Audit.Public;
using FreshOrbit.E1Testing;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FreshOrbit.Audit.Tests.Integration;

public sealed class AuditTests(E1Database database) : E1ApiTestBase(database)
{
    [Fact]
    public async Task Recorded_entries_are_listed_and_filtered_for_audit_viewers()
    {
        var entityId = Guid.CreateVersion7().ToString();
        await InScopeAsync(async provider =>
        {
            provider.GetRequiredService<IAuditApi>().Record(new AuditRecord("PRICE_CHANGED", "price", entityId, new { pricePaise = 1000 }, new { pricePaise = 1200 }));
            return await provider.GetRequiredService<FreshOrbitDbContext>().SaveChangesAsync(Ct);
        });

        using var viewer = Factory.CreateStaffClient(["audit.view"]);
        var response = await viewer.GetAsync($"/api/v1/admin/audit?entityType=price&entityId={entityId}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("action").GetString().Should().Be("PRICE_CHANGED");
        item.GetProperty("before").GetProperty("pricePaise").GetInt32().Should().Be(1000);
        item.GetProperty("after").GetProperty("pricePaise").GetInt32().Should().Be(1200);

        using var other = Factory.CreateStaffClient(["orders.view"]);
        (await other.GetAsync("/api/v1/admin/audit", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Entries_cannot_be_changed_or_deleted()
    {
        var entityId = Guid.CreateVersion7().ToString();
        await using var db = Database.CreateDbContext();
        db.Add(AuditEntry.Create(new AuditEntryData(DateTimeOffset.UtcNow, ActorType.System, null, null, "TEST_RECORDED", "test", entityId, null, null, null, null)));
        await db.SaveChangesAsync(Ct);

        var update = async () => await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE audit.entries SET action = 'CHANGED' WHERE entity_id = {entityId}", Ct);
        await update.Should().ThrowAsync<PostgresException>();
        var delete = async () => await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM audit.entries WHERE entity_id = {entityId}", Ct);
        await delete.Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task Invalid_list_query_is_refused()
    {
        using var viewer = Factory.CreateStaffClient(["audit.view"]);
        var response = await viewer.GetAsync("/api/v1/admin/audit?pageSize=500", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
