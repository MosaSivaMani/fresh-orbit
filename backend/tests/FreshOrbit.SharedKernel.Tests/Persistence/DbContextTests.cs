using AwesomeAssertions;
using FreshOrbit.SharedKernel.EntityFramework.Outbox;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Tests.TestModule;
using FreshOrbit.SharedKernel.Time;
using FreshOrbit.Testing;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.SharedKernel.Tests.Persistence;

public sealed class DbContextTests(SharedKernelDatabase database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Save_stamps_audit_columns_and_row_version()
    {
        var creator = TestActor.Staff();
        var createdAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var widget = Widget.Create("audit");
        await using (var db = database.CreateDbContext(creator, new TestClock(createdAt)))
        {
            db.Add(widget);
            await db.SaveChangesAsync(Ct);
        }

        widget.Version.Should().NotBe(0u);
        var created = await ReadAuditAsync(widget.Id);
        created.CreatedAt.Should().Be(createdAt);
        created.CreatedBy.Should().Be(creator.UserId);
        created.ModifiedAt.Should().BeNull();
        created.ModifiedBy.Should().BeNull();

        var modifier = TestActor.Staff();
        var modifiedAt = createdAt.AddHours(1);
        uint versionAfterUpdate;
        await using (var db = database.CreateDbContext(modifier, new TestClock(modifiedAt)))
        {
            var loaded = await db.Set<Widget>().SingleAsync(w => w.Id == widget.Id, Ct);
            loaded.Version.Should().Be(widget.Version);
            loaded.Rename("audit-2");
            await db.SaveChangesAsync(Ct);
            versionAfterUpdate = loaded.Version;
        }

        versionAfterUpdate.Should().NotBe(widget.Version);
        var modified = await ReadAuditAsync(widget.Id);
        modified.CreatedAt.Should().Be(createdAt);
        modified.CreatedBy.Should().Be(creator.UserId);
        modified.ModifiedAt.Should().Be(modifiedAt);
        modified.ModifiedBy.Should().Be(modifier.UserId);
    }

    [Fact]
    public async Task Enums_are_stored_as_upper_snake_codes_and_times_as_utc()
    {
        var widget = Widget.Create("codes");
        var reviewedAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.FromHours(5.5));
        widget.SubmitForReview(reviewedAt);
        await using (var db = database.CreateDbContext())
        {
            db.Add(widget);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        var status = await read.Database.SqlQuery<string>($"SELECT status AS \"Value\" FROM test.widgets WHERE id = {widget.Id}").SingleAsync(Ct);
        status.Should().Be("IN_REVIEW");
        var loaded = await read.Set<Widget>().SingleAsync(w => w.Id == widget.Id, Ct);
        loaded.Status.Should().Be(WidgetStatus.InReview);
        loaded.ReviewedAt.Should().Be(reviewedAt);
        loaded.ReviewedAt!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task A_time_from_the_system_clock_reads_back_equal()
    {
        // Linux clocks fill all 100 ns ticks; Postgres keeps microseconds, so the clock cuts them (StoredPrecision).
        var reviewedAt = new SystemClock().UtcNow;
        var widget = Widget.Create("precision");
        widget.SubmitForReview(reviewedAt);
        await using (var db = database.CreateDbContext())
        {
            db.Add(widget);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        (await read.Set<Widget>().SingleAsync(w => w.Id == widget.Id, Ct)).ReviewedAt.Should().Be(reviewedAt);
    }

    [Fact]
    public async Task SetExpectedVersion_with_a_stale_version_throws_ConcurrencyException()
    {
        var widget = await CreateWidgetAsync("stale");
        await using var db = database.CreateDbContext();
        var loaded = await db.Set<Widget>().SingleAsync(w => w.Id == widget.Id, Ct);

        db.SetExpectedVersion(loaded, loaded.Version);
        var stale = () => db.SetExpectedVersion(loaded, loaded.Version + 1);

        stale.Should().Throw<ConcurrencyException>().Which.Code.Should().Be("CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task Lost_race_throws_ConcurrentSaveException_and_writes_no_outbox_row()
    {
        var widget = await CreateWidgetAsync("race");
        await using var first = database.CreateDbContext();
        await using var second = database.CreateDbContext();
        var firstCopy = await first.Set<Widget>().SingleAsync(w => w.Id == widget.Id, Ct);
        var secondCopy = await second.Set<Widget>().SingleAsync(w => w.Id == widget.Id, Ct);

        firstCopy.Rename("winner");
        await first.SaveChangesAsync(Ct);
        secondCopy.Rename("loser");
        var losingEventId = secondCopy.DomainEvents.Single().EventId;
        var save = () => second.SaveChangesAsync(Ct);

        (await save.Should().ThrowAsync<ConcurrentSaveException>()).Which.Code.Should().Be("CONCURRENT_SAVE");
        await using var read = database.CreateDbContext();
        (await read.Set<OutboxMessage>().AnyAsync(m => m.Id == losingEventId, Ct)).Should().BeFalse();
        (await read.Set<Widget>().SingleAsync(w => w.Id == widget.Id, Ct)).Name.Should().Be("winner");
    }

    [Fact]
    public async Task Save_writes_raised_events_to_the_outbox_in_the_same_transaction()
    {
        var widget = Widget.Create("outbox");
        var eventId = widget.DomainEvents.Single().EventId;
        await using (var db = database.CreateDbContext())
        {
            db.Add(widget);
            await db.SaveChangesAsync(Ct);
        }

        widget.DomainEvents.Should().BeEmpty();
        await using var read = database.CreateDbContext();
        var message = await read.Set<OutboxMessage>().SingleAsync(m => m.Id == eventId, Ct);
        message.EventType.Should().Be(EventTypeNames.Format(typeof(WidgetCreated)));
        message.Payload.Should().Contain(widget.Id.ToString());
        message.ProcessedAt.Should().BeNull();
        message.Attempts.Should().Be(0);
        EventTypeNames.Resolve(message.EventType).Should().Be<WidgetCreated>();
    }

    [Fact]
    public async Task Rolled_back_transaction_leaves_neither_rows_nor_outbox_messages()
    {
        var widget = Widget.Create("rollback");
        var eventId = widget.DomainEvents.Single().EventId;
        await using (var db = database.CreateDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            db.Add(widget);
            await db.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        (await read.Set<Widget>().AnyAsync(w => w.Id == widget.Id, Ct)).Should().BeFalse();
        (await read.Set<OutboxMessage>().AnyAsync(m => m.Id == eventId, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Schema_has_every_module_schema_and_required_extensions()
    {
        await using var db = database.CreateDbContext();
        var schemas = await db.Database.SqlQuery<string>($"SELECT schema_name AS \"Value\" FROM information_schema.schemata").ToListAsync(Ct);
        schemas.Should().Contain(["platform", "test"]);
        var extensions = await db.Database.SqlQuery<string>($"SELECT extname AS \"Value\" FROM pg_extension").ToListAsync(Ct);
        extensions.Should().Contain(["citext", "pg_trgm", "btree_gist"]);
        db.ModuleModels.Select(m => m.Schema).Should().Contain(
        [
            "identity", "customers", "locations", "catalog", "pricing", "inventory", "cart", "promotions",
            "ordering", "payments", "delivery", "notifications", "reporting", "audit", "settings", "platform",
        ]);
    }

    private async Task<Widget> CreateWidgetAsync(string name)
    {
        var widget = Widget.Create(name);
        await using var db = database.CreateDbContext();
        db.Add(widget);
        await db.SaveChangesAsync(Ct);
        return widget;
    }

    private async Task<AuditRow> ReadAuditAsync(Guid id)
    {
        await using var db = database.CreateDbContext();
        return await db.Set<Widget>().Where(w => w.Id == id).Select(w => new AuditRow(
                EF.Property<DateTimeOffset>(w, "CreatedAt"),
                EF.Property<Guid?>(w, "CreatedBy"),
                EF.Property<DateTimeOffset?>(w, "ModifiedAt"),
                EF.Property<Guid?>(w, "ModifiedBy")))
            .SingleAsync(Ct);
    }

    private sealed record AuditRow(DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset? ModifiedAt, Guid? ModifiedBy);
}
