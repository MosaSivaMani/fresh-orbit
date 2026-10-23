using AwesomeAssertions;
using FreshOrbit.Notifications.Application.Queueing;
using FreshOrbit.Notifications.Domain.Messages;
using FreshOrbit.Notifications.Domain.Preferences;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.Notifications.Domain.Templates;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Notifications.Tests.Unit;

public sealed class TemplateTextTests
{
    [Fact]
    public void Placeholders_are_replaced_and_missing_values_render_empty()
        => TemplateText.Render("Order {{orderNumber}} for {{ recipientName }}{{missing}}.", new Dictionary<string, string> { ["orderNumber"] = "FO-1", ["recipientName"] = "Asha" })
            .Should().Be("Order FO-1 for Asha.");

    [Fact]
    public void Placeholders_are_listed() => TemplateText.Placeholders("{{a}} and {{b}} {{a}}").Should().Equal("a", "b", "a");
}

public sealed class TemplateTests
{
    [Fact]
    public void Unknown_placeholder_is_refused_but_common_variables_are_allowed()
    {
        var template = Template.Create("ORDER_PREPARING", Channel.InApp, Category.OrderUpdates, null, "{{orderNumber}} at {{businessName}}", ["orderNumber"], null);

        template.Invoking(t => t.Edit(null, "{{orderNumber}} {{riderName}}", true)).Should().Throw<DomainException>()
            .Which.Code.Should().Be(NotificationsErrors.TemplatePlaceholderUnknown);
        template.Body.Should().Be("{{orderNumber}} at {{businessName}}");
    }

    [Fact]
    public void Email_and_push_need_a_subject()
        => FluentActions.Invoking(() => Template.Create("ORDER_PACKED", Channel.Push, Category.OrderUpdates, null, "x", [], null))
            .Should().Throw<RefusalException>().Which.Errors.Should().ContainSingle(e => e.Field == "subject");
}

public sealed class PreferenceRuleTests
{
    private static Template Of(Channel channel, Category? category) => Template.Create("X", channel, category, channel is Channel.Email or Channel.Push ? "s" : null, "b", [], null);

    [Fact]
    public void Order_update_sms_cannot_be_disabled()
    {
        FluentActions.Invoking(() => Preference.EnsureAllowed(Category.OrderUpdates, Channel.Sms, false)).Should().Throw<DomainException>()
            .Which.Code.Should().Be(NotificationsErrors.PreferenceRequired);
        var stored = new[] { Preference.Create(Guid.Empty, Category.OrderUpdates, Channel.Sms, false) };
        NotificationComposer.IsAllowed(Of(Channel.Sms, Category.OrderUpdates), stored).Should().BeTrue();
    }

    [Fact]
    public void Missing_rows_mean_on_for_order_updates_and_off_for_offers()
    {
        NotificationComposer.IsAllowed(Of(Channel.Push, Category.OrderUpdates), []).Should().BeTrue();
        NotificationComposer.IsAllowed(Of(Channel.Push, Category.Offers), []).Should().BeFalse();
    }

    [Fact]
    public void Stored_preference_wins_and_templates_without_category_ignore_preferences()
    {
        var stored = new[] { Preference.Create(Guid.Empty, Category.OrderUpdates, Channel.Push, false) };
        NotificationComposer.IsAllowed(Of(Channel.Push, Category.OrderUpdates), stored).Should().BeFalse();
        NotificationComposer.IsAllowed(Of(Channel.Email, null), stored).Should().BeTrue();
    }
}

public sealed class MessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void In_app_messages_start_sent_and_can_be_read_once()
    {
        var message = Message.Queue(Guid.CreateVersion7(), Channel.InApp, "ORDER_PACKED", "t", "b", new Dictionary<string, string>(), "evt", Now);

        message.Status.Should().Be(MessageStatus.Sent);
        message.DedupeKey.Should().Be($"evt:IN_APP:{message.UserId}");
        message.MarkRead(Now).Should().BeTrue();
        message.MarkRead(Now).Should().BeFalse();
        message.Status.Should().Be(MessageStatus.Read);
    }

    [Fact]
    public void Provider_errors_back_off_and_fail_after_five_attempts()
    {
        var message = Message.Queue(Guid.CreateVersion7(), Channel.Sms, "ORDER_PACKED", null, "b", new Dictionary<string, string>(), null, Now);
        for (var i = 1; i < NotificationsErrors.MaxAttempts; i++)
        {
            message.RecordFailure("timeout", Now);
            message.Status.Should().Be(MessageStatus.Queued);
            message.NextAttemptAt.Should().Be(Now.AddMinutes(i));
        }

        message.RecordFailure("timeout", Now);
        message.Status.Should().Be(MessageStatus.Failed);
        message.Attempts.Should().Be(NotificationsErrors.MaxAttempts);
    }

    [Fact]
    public void Sms_cannot_be_marked_read()
    {
        var message = Message.Queue(Guid.CreateVersion7(), Channel.Sms, "X", null, "b", new Dictionary<string, string>(), null, Now);
        message.MarkSent("p-1", Now);

        message.MarkRead(Now).Should().BeFalse();
        message.Status.Should().Be(MessageStatus.Sent);
    }
}
