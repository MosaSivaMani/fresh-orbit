using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.E1Testing;
using FreshOrbit.Notifications.Application.Sending;
using FreshOrbit.Notifications.Domain.Devices;
using FreshOrbit.Notifications.Domain.Messages;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.Notifications.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Notifications.Tests.Integration;

public sealed class NotificationsTests(E1Database database) : E1ApiTestBase(database)
{
    private static readonly Dictionary<string, string> OrderData = new()
    {
        ["orderNumber"] = "FO-260042",
        ["slotDateText"] = "24 Oct",
        ["slotWindowText"] = "2:00–4:00 PM",
        ["estimatedTotalText"] = "₹450.00",
        ["paymentMethodText"] = "Cash on delivery",
        ["orderUrl"] = "https://freshorbit.in/orders/1",
    };

    [Fact]
    public async Task Duplicate_event_queues_each_channel_once()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        var customerId = await CustomerIdAsync(session.UserId);
        var eventId = Guid.CreateVersion7().ToString();

        for (var delivery = 0; delivery < 2; delivery++)
        {
            await QueueAsync(new QueueCustomerNotificationRequest(customerId, "ORDER_CONFIRMED", OrderData, null, eventId));
        }

        // The same event handled twice inside one unit of work is also deduplicated.
        await InScopeAsync(async provider =>
        {
            var api = provider.GetRequiredService<INotificationsApi>();
            await api.QueueForCustomerAsync(new QueueCustomerNotificationRequest(customerId, "ORDER_CONFIRMED", OrderData, null, eventId), Ct);
            await api.QueueForCustomerAsync(new QueueCustomerNotificationRequest(customerId, "ORDER_CONFIRMED", OrderData, null, eventId), Ct);
            return await provider.GetRequiredService<FreshOrbitDbContext>().SaveChangesAsync(Ct);
        });

        var messages = await MessagesAsync(session.UserId);
        messages.Select(m => m.Channel).Should().BeEquivalentTo([Channel.Sms, Channel.Email, Channel.Push, Channel.InApp]);
        messages.Single(m => m.Channel == Channel.Sms).Body.Should().Contain("FO-260042").And.Contain("2:00–4:00 PM");
    }

    [Fact]
    public async Task Worker_sends_sms_and_push_and_fails_without_an_address()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);
        var pushToken = "fcm-" + Guid.NewGuid().ToString("N");
        var registered = await client.PutAsJsonAsync("/api/v1/me/device-tokens", new { pushToken, provider = "FCM", platform = "ANDROID" }, Ct);
        registered.StatusCode.Should().Be(HttpStatusCode.OK);

        await QueueAsync(new QueueCustomerNotificationRequest(await CustomerIdAsync(session.UserId), "ORDER_CONFIRMED", OrderData, null, Guid.CreateVersion7().ToString()));
        await DispatchAllAsync();

        var messages = await MessagesAsync(session.UserId);
        messages.Single(m => m.Channel == Channel.Sms).Status.Should().Be(MessageStatus.Sent);
        messages.Single(m => m.Channel == Channel.Push).Status.Should().Be(MessageStatus.Sent);
        var email = messages.Single(m => m.Channel == Channel.Email);
        email.Status.Should().Be(MessageStatus.Failed);
        email.LastError.Should().Be(NotificationsErrors.NoRecipientAddress);
        Factory.Sms.Sent.Should().Contain(s => s.Phone == session.Phone && s.Text.Contains("FO-260042"));
        Factory.Push.Sent.Should().Contain(p => p.Tokens.Contains(pushToken));
    }

    [Fact]
    public async Task Push_provider_errors_are_retried_later()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);
        await client.PutAsJsonAsync("/api/v1/me/device-tokens", new { pushToken = "fcm-" + Guid.NewGuid().ToString("N"), provider = "FCM", platform = "IOS" }, Ct);
        await QueueAsync(new QueueCustomerNotificationRequest(await CustomerIdAsync(session.UserId), "ORDER_PREPARING", new Dictionary<string, string> { ["orderNumber"] = "FO-1" }, [NotificationChannel.Push], null));

        Factory.Push.Fail = true;
        await DispatchAllAsync();
        var failed = (await MessagesAsync(session.UserId)).Single();
        failed.Status.Should().Be(MessageStatus.Queued);
        failed.Attempts.Should().Be(1);

        Factory.Push.Fail = false;
        await DispatchAllAsync();
        (await MessagesAsync(session.UserId)).Single().Status.Should().Be(MessageStatus.Queued, "the retry waits one minute");

        Factory.Clock.Advance(TimeSpan.FromMinutes(2));
        await DispatchAllAsync();
        (await MessagesAsync(session.UserId)).Single().Status.Should().Be(MessageStatus.Sent);
    }

    [Fact]
    public async Task In_app_inbox_lists_and_marks_read()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        var customerId = await CustomerIdAsync(session.UserId);
        for (var i = 0; i < 3; i++)
        {
            await QueueAsync(new QueueCustomerNotificationRequest(customerId, "ORDER_PREPARING", new Dictionary<string, string> { ["orderNumber"] = $"FO-{i}" }, [NotificationChannel.InApp], null));
        }

        using var client = Factory.CreateClient(session.AccessToken);
        var inbox = await ReadJsonAsync(await client.GetAsync("/api/v1/me/notifications", Ct));
        inbox.GetProperty("totalCount").GetInt32().Should().Be(3);
        inbox.GetProperty("unreadCount").GetInt32().Should().Be(3);
        var firstId = inbox.GetProperty("items")[0].GetProperty("messageId").GetGuid();

        (await client.PostAsync($"/api/v1/me/notifications/{firstId}/read", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/v1/me/notifications/{firstId}/read", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadJsonAsync(await client.GetAsync("/api/v1/me/notifications?unreadOnly=true", Ct))).GetProperty("totalCount").GetInt32().Should().Be(2);

        (await client.PostAsync("/api/v1/me/notifications/read-all", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadJsonAsync(await client.GetAsync("/api/v1/me/notifications", Ct))).GetProperty("unreadCount").GetInt32().Should().Be(0);

        // Someone else's message is not found.
        var other = await Factory.SignInCustomerAsync(null, Ct);
        using var otherClient = Factory.CreateClient(other.AccessToken);
        (await otherClient.PostAsync($"/api/v1/me/notifications/{firstId}/read", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Preferences_filter_channels_but_order_sms_stays_on()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);

        var refused = await client.PutAsJsonAsync("/api/v1/me/notification-preferences", new { preferences = new[] { new { category = "ORDER_UPDATES", channel = "SMS", enabled = false } } }, Ct);
        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorCodeAsync(refused)).Should().Be("PREFERENCE_REQUIRED");

        var saved = await client.PutAsJsonAsync("/api/v1/me/notification-preferences", new { preferences = new[] { new { category = "ORDER_UPDATES", channel = "PUSH", enabled = false }, new { category = "ORDER_UPDATES", channel = "EMAIL", enabled = false } } }, Ct);
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(saved)).GetProperty("preferences").GetArrayLength().Should().Be(8);

        await QueueAsync(new QueueCustomerNotificationRequest(await CustomerIdAsync(session.UserId), "ORDER_CONFIRMED", OrderData, null, Guid.CreateVersion7().ToString()));
        (await MessagesAsync(session.UserId)).Select(m => m.Channel).Should().BeEquivalentTo([Channel.Sms, Channel.InApp]);
    }

    [Fact]
    public async Task Device_tokens_follow_ruling_d28_and_move_between_users()
    {
        var first = await Factory.SignInCustomerAsync(null, Ct);
        var second = await Factory.SignInCustomerAsync(null, Ct);
        using var firstClient = Factory.CreateClient(first.AccessToken);
        using var secondClient = Factory.CreateClient(second.AccessToken);
        var pushToken = "fcm-" + Guid.NewGuid().ToString("N");

        var expo = await firstClient.PutAsJsonAsync("/api/v1/me/device-tokens", new { pushToken, provider = "EXPO", platform = "ANDROID" }, Ct);
        expo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await firstClient.PutAsJsonAsync("/api/v1/me/device-tokens", new { pushToken, provider = "FCM", platform = "WEB" }, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await secondClient.PutAsJsonAsync("/api/v1/me/device-tokens", new { pushToken, provider = "FCM", platform = "ANDROID" }, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await QueryAsync(db => db.Set<DeviceToken>().SingleAsync(t => t.PushToken == pushToken, Ct));
        token.UserId.Should().Be(second.UserId);
        token.Platform.Should().Be(DevicePlatform.Android);

        // Only the owner can unregister.
        (await firstClient.DeleteAsync($"/api/v1/me/device-tokens/{pushToken}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await QueryAsync(db => db.Set<DeviceToken>().AnyAsync(t => t.PushToken == pushToken, Ct))).Should().BeTrue();
        (await secondClient.DeleteAsync($"/api/v1/me/device-tokens/{pushToken}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await QueryAsync(db => db.Set<DeviceToken>().AnyAsync(t => t.PushToken == pushToken, Ct))).Should().BeFalse();
    }

    [Fact]
    public async Task Template_edits_are_checked_and_need_notifications_manage()
    {
        using var other = Factory.CreateStaffClient(["orders.view"]);
        (await other.GetAsync("/api/v1/admin/notification-templates", Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var admin = Factory.CreateStaffClient(["notifications.manage"]);
        var templates = await ReadJsonAsync(await admin.GetAsync("/api/v1/admin/notification-templates", Ct));
        templates.GetArrayLength().Should().Be(54);
        var template = templates.EnumerateArray().Single(t => t.GetProperty("code").GetString() == "ORDER_PACKED" && t.GetProperty("channel").GetString() == "IN_APP");

        var bad = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/notification-templates/ORDER_PACKED/IN_APP")
        {
            Content = JsonContent.Create(new { subject = "Packed", body = "{{orderNumber}} by {{riderName}}", isActive = true }),
        };
        bad.Headers.IfMatch.ParseAdd($"\"{template.GetProperty("version").GetString()}\"");
        var refused = await admin.SendAsync(bad, Ct);
        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorCodeAsync(refused)).Should().Be("TEMPLATE_PLACEHOLDER_UNKNOWN");

        var test = await admin.PostAsync("/api/v1/admin/notification-templates/ORDER_PACKED/IN_APP/test", null, Ct);
        test.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(test)).GetProperty("body").GetString().Should().Contain("[orderNumber]");
    }

    [Fact]
    public async Task Reference_data_reload_keeps_staff_edits()
    {
        using var admin = Factory.CreateStaffClient(["notifications.manage"]);
        var templates = await ReadJsonAsync(await admin.GetAsync("/api/v1/admin/notification-templates", Ct));
        var template = templates.EnumerateArray().Single(t => t.GetProperty("code").GetString() == "DELIVERY_FAILED" && t.GetProperty("channel").GetString() == "IN_APP");
        var edit = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/notification-templates/DELIVERY_FAILED/IN_APP")
        {
            Content = JsonContent.Create(new { subject = "Delivery missed", body = "Sorry, {{orderNumber}}: {{reasonText}}", isActive = true }),
        };
        edit.Headers.IfMatch.ParseAdd($"\"{template.GetProperty("version").GetString()}\"");
        (await admin.SendAsync(edit, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        await Database.LoadReferenceDataAsync();

        var after = await ReadJsonAsync(await admin.GetAsync("/api/v1/admin/notification-templates", Ct));
        after.EnumerateArray().Single(t => t.GetProperty("code").GetString() == "DELIVERY_FAILED" && t.GetProperty("channel").GetString() == "IN_APP")
            .GetProperty("body").GetString().Should().Be("Sorry, {{orderNumber}}: {{reasonText}}");
    }

    private Task<int> QueueAsync(QueueCustomerNotificationRequest request)
        => InScopeAsync(async provider =>
        {
            await provider.GetRequiredService<INotificationsApi>().QueueForCustomerAsync(request, Ct);
            return await provider.GetRequiredService<FreshOrbitDbContext>().SaveChangesAsync(Ct);
        });

    private async Task DispatchAllAsync()
    {
        while (await InScopeAsync(provider => provider.GetRequiredService<MessageDispatcher>().SendBatchAsync(Ct)) > 0)
        {
        }
    }

    private Task<Guid> CustomerIdAsync(Guid userId) => QueryAsync(db => db.Set<Customer>().Where(c => c.UserId == userId).Select(c => c.Id).SingleAsync(Ct));

    private Task<List<Message>> MessagesAsync(Guid userId) => QueryAsync(db => db.Set<Message>().AsNoTracking().Where(m => m.UserId == userId).ToListAsync(Ct));
}
