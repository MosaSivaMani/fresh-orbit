using AwesomeAssertions;
using FreshOrbit.Notifications.Infrastructure.Senders;
using FreshOrbit.Notifications.Public;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Notifications.Tests.Unit;

/// <summary>D358: the store readiness page learns only each channel's provider code and whether it really delivers.</summary>
public sealed class NotificationProvidersTests
{
    [Fact]
    public void Empty_settings_are_reported_as_development_senders_that_do_not_deliver()
    {
        var status = Providers(new SmsOptions(), new EmailOptions(), new SmtpOptions(), new PushOptions()).GetStatus();

        status.Sms.Should().Be(new ChannelProviderStatus("DEVELOPMENT", false));
        status.Email.Should().Be(new ChannelProviderStatus("DEVELOPMENT", false));
        status.Push.Should().Be(new ChannelProviderStatus("DEVELOPMENT", false));
    }

    [Fact]
    public void Real_providers_deliver_and_an_smtp_host_alone_means_smtp()
    {
        var status = Providers(
            new SmsOptions { Provider = "DLT", Dlt = new DltSmsOptions { ApiKey = "secret-key" } },
            new EmailOptions(),
            new SmtpOptions { Host = "smtp.example.invalid", Password = "secret-password" },
            new PushOptions { Provider = "FCM", Fcm = new FcmOptions { ServiceAccountJson = "{\"private_key\":\"secret\"}" } }).GetStatus();

        status.Sms.Should().Be(new ChannelProviderStatus("DLT", true));
        status.Email.Should().Be(new ChannelProviderStatus("SMTP", true));
        status.Push.Should().Be(new ChannelProviderStatus("FCM", true));
        status.ToString().Should().NotContain("secret");
    }

    [Fact]
    public void Disabled_channels_are_reported_as_not_delivering()
    {
        var status = Providers(new SmsOptions(), new EmailOptions { Provider = "DISABLED" }, new SmtpOptions { Host = "smtp.example.invalid" }, new PushOptions { Provider = "DISABLED" }).GetStatus();

        status.Email.Should().Be(new ChannelProviderStatus("DISABLED", false));
        status.Push.Should().Be(new ChannelProviderStatus("DISABLED", false));
    }

    private static NotificationProviders Providers(SmsOptions sms, EmailOptions email, SmtpOptions smtp, PushOptions push)
        => new(Options.Create(sms), Options.Create(email), Options.Create(smtp), Options.Create(push));
}
