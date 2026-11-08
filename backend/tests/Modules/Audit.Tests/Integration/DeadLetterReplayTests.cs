using System.Net;
using AwesomeAssertions;
using FreshOrbit.Audit.Domain.Entries;
using FreshOrbit.E1Testing;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Audit.Tests.Integration;

/// <summary>BE-2 / D296: staff with <c>platform.manage</c> list dead letters and put their event back in the outbox.</summary>
public sealed class DeadLetterReplayTests(E1Database database) : E1ApiTestBase(database)
{
    private const string Permission = "platform.manage";

    [Fact]
    public async Task Replay_puts_the_event_back_in_the_queue_once_marks_its_dead_letters_and_is_audited()
    {
        var (eventId, deadLetterId, siblingId) = await SeedDeadLetteredEventAsync();
        var staffUserId = Guid.CreateVersion7();
        using var operations = Factory.CreateStaffClient([Permission], userId: staffUserId);

        var open = await operations.GetAsync("/api/v1/admin/dead-letters?status=OPEN&pageSize=100", Ct);
        open.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadJsonAsync(open);
        page.GetProperty("page").GetInt32().Should().Be(1);
        page.GetProperty("pageSize").GetInt32().Should().Be(100);
        page.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        var listed = page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == deadLetterId);
        listed.GetProperty("eventId").GetGuid().Should().Be(eventId);
        listed.GetProperty("eventType").GetString().Should().Be(EventType);
        listed.GetProperty("handler").GetString().Should().Be("Tests.FailingHandler");
        listed.GetProperty("attempts").GetInt32().Should().Be(5);
        listed.GetProperty("replayedAt").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        var replay = await operations.PostAsync($"/api/v1/admin/dead-letters/{deadLetterId}/replay", null, Ct);

        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        var replayed = await ReadJsonAsync(replay);
        replayed.GetProperty("id").GetGuid().Should().Be(deadLetterId);
        replayed.GetProperty("attempts").GetInt32().Should().Be(0);
        replayed.GetProperty("replayedAt").ValueKind.Should().Be(System.Text.Json.JsonValueKind.String);
        replayed.GetProperty("replayedBy").GetGuid().Should().Be(staffUserId);

        await using (var db = Database.CreateDbContext())
        {
            var message = await db.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.Id == eventId, Ct);
            message.Attempts.Should().Be(0);
            message.NextAttemptAt.Should().BeNull("a replayed event is due on the next poll");
            message.ProcessedAt.Should().BeNull();
            var letters = await db.Set<DeadLetter>().AsNoTracking().Where(d => d.EventId == eventId).ToListAsync(Ct);
            letters.Should().HaveCount(2).And.OnlyContain(d => d.ReplayedAt != null && d.ReplayedBy == staffUserId);
            var entry = await db.Set<AuditEntry>().AsNoTracking().SingleAsync(e => e.Action == "DEAD_LETTER_REPLAYED" && e.EntityId == deadLetterId.ToString(), Ct);
            entry.ActorType.Should().Be(ActorType.Staff);
            entry.EntityType.Should().Be("DEAD_LETTER");
        }

        var again = await operations.PostAsync($"/api/v1/admin/dead-letters/{deadLetterId}/replay", null, Ct);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(again)).Should().Be("DEAD_LETTER_ALREADY_REPLAYED");
        var sibling = await operations.PostAsync($"/api/v1/admin/dead-letters/{siblingId}/replay", null, Ct);
        sibling.StatusCode.Should().Be(HttpStatusCode.Conflict, "the sibling handler's dead letter was replayed with the same event");
        (await ErrorCodeAsync(sibling)).Should().Be("DEAD_LETTER_ALREADY_REPLAYED");

        var replayedList = await ReadJsonAsync(await operations.GetAsync("/api/v1/admin/dead-letters?status=REPLAYED&pageSize=100", Ct));
        replayedList.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Contain(deadLetterId);
        var stillOpen = await ReadJsonAsync(await operations.GetAsync("/api/v1/admin/dead-letters?status=OPEN&pageSize=100", Ct));
        stillOpen.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().NotContain(deadLetterId);
    }

    [Fact]
    public async Task Replay_and_list_are_refused_with_problem_codes()
    {
        var (_, deadLetterId, _) = await SeedDeadLetteredEventAsync();
        using var operations = Factory.CreateStaffClient([Permission]);
        using var auditor = Factory.CreateStaffClient(["audit.view"]);

        var unknown = await operations.PostAsync($"/api/v1/admin/dead-letters/{Guid.CreateVersion7()}/replay", null, Ct);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(unknown)).Should().Be("DEAD_LETTER_NOT_FOUND");

        (await auditor.PostAsync($"/api/v1/admin/dead-letters/{deadLetterId}/replay", null, Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await auditor.GetAsync("/api/v1/admin/dead-letters", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Factory.CreateClient().GetAsync("/api/v1/admin/dead-letters", Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var badStatus = await operations.GetAsync("/api/v1/admin/dead-letters?status=LOST", Ct);
        badStatus.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadJsonAsync(badStatus)).GetProperty("errors").TryGetProperty("status", out _).Should().BeTrue();

        await using var db = Database.CreateDbContext();
        (await db.Set<DeadLetter>().AsNoTracking().SingleAsync(d => d.Id == deadLetterId, Ct)).ReplayedAt.Should().BeNull("refused replays change nothing");
    }

    private const string EventType = "FreshOrbit.Tests.GoneEvent, FreshOrbit.Tests";

    /// <summary>An outbox row whose two handlers failed every attempt (as the dispatcher leaves it), with its dead letters.</summary>
    private async Task<(Guid EventId, Guid DeadLetterId, Guid SiblingId)> SeedDeadLetteredEventAsync()
    {
        var eventId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await using var db = Database.CreateDbContext();
        db.Add(new OutboxMessage(eventId, EventType, "{}", now.AddHours(-7), null) { Attempts = 5, Error = "Tests.FailingHandler: down" });
        var deadLetter = new DeadLetter(eventId, "Tests.FailingHandler", "System.InvalidOperationException: down", now);
        var sibling = new DeadLetter(eventId, "Tests.OtherFailingHandler", "System.InvalidOperationException: down", now);
        db.AddRange(deadLetter, sibling);
        await db.SaveChangesAsync(Ct);
        return (eventId, deadLetter.Id, sibling.Id);
    }
}
