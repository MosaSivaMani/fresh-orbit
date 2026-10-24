using AwesomeAssertions;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Notifications.Infrastructure.Senders;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Notifications.Tests.Unit;

/// <summary>SEC-1: phone masking and the DLT adapter stub.</summary>
public sealed class SmsSenderUnitTests
{
    [Theory]
    [InlineData("+919876543210", "***3210")]
    [InlineData("12345", "***2345")]
    [InlineData("1234", "****")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Mask_keeps_only_the_last_four_digits(string? phone, string expected)
        => PhoneMasking.Mask(phone).Should().Be(expected);

    [Fact]
    public void Dlt_sender_lists_every_missing_setting()
    {
        var sender = Dlt(new DltSmsOptions { BaseUrl = "http://insecure.example", ApiKey = "key" });

        sender.MissingSettings().Should().BeEquivalentTo(
            "Notifications:Sms:Dlt:BaseUrl (https)",
            "Notifications:Sms:Dlt:SenderId",
            "Notifications:Sms:Dlt:PrincipalEntityId");
    }

    [Fact]
    public async Task Dlt_sender_refuses_to_send_when_not_configured()
    {
        var sender = Dlt(new DltSmsOptions());

        var send = () => sender.SendAsync(new SmsMessage("+919876543210", "Your code is 123456", "1107000000000000001"), CancellationToken.None);

        await send.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Dlt_sender_refuses_a_message_without_a_dlt_template_id()
    {
        var sender = Dlt(Configured());

        var send = () => sender.SendAsync(new SmsMessage("+919876543210", "Your code is 123456", null), CancellationToken.None);

        await send.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Configured_dlt_stub_never_reports_a_send_it_did_not_make()
    {
        var sender = Dlt(Configured());
        sender.MissingSettings().Should().BeEmpty();

        var send = () => sender.SendAsync(new SmsMessage("+919876543210", "Your code is 123456", "1107000000000000001"), CancellationToken.None);

        await send.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void Development_outbox_keeps_the_newest_messages_per_phone()
    {
        var outbox = new DevelopmentSmsOutbox();
        for (var i = 0; i < DevelopmentSmsOutbox.Capacity + 5; i++)
        {
            outbox.Add(new DevelopmentSms("+919800000001", $"message {i}", null, DateTimeOffset.UnixEpoch.AddSeconds(i)));
        }

        var kept = outbox.For("+919800000001");

        kept.Should().HaveCount(DevelopmentSmsOutbox.Capacity);
        kept[0].Text.Should().Be($"message {DevelopmentSmsOutbox.Capacity + 4}");
        outbox.For("+919800000002").Should().BeEmpty();
    }

    private static DltSmsOptions Configured() => new()
    {
        BaseUrl = "https://sms.example.in/api",
        ApiKey = "key",
        SenderId = "FRSHOR",
        PrincipalEntityId = "1101000000000000001",
    };

    private static DltSmsSender Dlt(DltSmsOptions dlt) => new(new StaticOptionsMonitor(new SmsOptions { Provider = SmsOptions.DltProvider, Dlt = dlt }));

    private sealed class StaticOptionsMonitor(SmsOptions value) : IOptionsMonitor<SmsOptions>
    {
        public SmsOptions CurrentValue => value;

        public SmsOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<SmsOptions, string?> listener) => null;
    }
}
