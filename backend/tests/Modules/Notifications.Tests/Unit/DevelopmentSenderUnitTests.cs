using AwesomeAssertions;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.Notifications.Infrastructure.Senders;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Notifications.Tests.Unit;

/// <summary>
/// BE-4 (D322, D323): the development senders never log a full e-mail address and record that nothing was delivered;
/// the DISABLED senders refuse with the channel's code.
/// </summary>
public sealed class DevelopmentSenderUnitTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("ravi.kumar@example.com", "r***@example.com")]
    [InlineData("a@b.in", "a***@b.in")]
    [InlineData("no-at-sign", "***")]
    [InlineData("@example.com", "***")]
    [InlineData("ravi@", "***")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Mask_keeps_only_the_first_character_and_the_domain(string? email, string expected)
        => EmailMasking.Mask(email).Should().Be(expected);

    [Fact]
    public async Task Logging_email_sender_logs_the_masked_address_and_records_not_delivered()
    {
        var logger = new CapturingLogger<LoggingEmailSender>();

        var id = await new LoggingEmailSender(logger).SendAsync(new EmailMessage("ravi.kumar@example.com", "Order FO-1 confirmed", "Body text"), Ct);

        id.Should().Be(DevelopmentDelivery.NotDelivered);
        logger.Lines.Should().ContainSingle().Which.Should().Contain("r***@example.com").And.NotContain("ravi.kumar").And.NotContain("Body text");
    }

    [Fact]
    public async Task Logging_push_sender_records_not_delivered()
    {
        var result = await new LoggingPushSender(new CapturingLogger<LoggingPushSender>()).SendAsync(new PushMessage(["device-a"], "Title", "Body", new Dictionary<string, string>()), Ct);

        result.ProviderMessageId.Should().Be(DevelopmentDelivery.NotDelivered);
    }

    [Fact]
    public async Task Disabled_senders_refuse_with_the_channel_code()
    {
        var email = () => new DisabledEmailSender().SendAsync(new EmailMessage("a@b.in", "S", "B"), Ct);
        var push = () => new DisabledPushSender().SendAsync(new PushMessage(["device-a"], "T", "B", new Dictionary<string, string>()), Ct);

        (await email.Should().ThrowAsync<ChannelDisabledException>()).Which.FailureCode.Should().Be("EMAIL_DISABLED");
        (await push.Should().ThrowAsync<ChannelDisabledException>()).Which.Channel.Should().Be(Channel.Push);
        new ChannelDisabledException(Channel.Push).FailureCode.Should().Be("PUSH_DISABLED");
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            Lines.Add(formatter(state, exception));
        }
    }
}
