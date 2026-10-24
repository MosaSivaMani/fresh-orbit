using FreshOrbit.Notifications.Application.Shared;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FreshOrbit.Notifications.Infrastructure.Senders;

/// <summary>Configuration section <c>Notifications:Smtp</c>. No <see cref="Host"/> → e-mails are only logged.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Notifications:Smtp";

    public string? Host { get; set; }

    /// <summary>Mailpit listens on 1025 in development.</summary>
    public int Port { get; set; } = 1025;

    public bool UseTls { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "no-reply@freshorbit.local";

    public string FromName { get; set; } = "Fresh Orbitt";
}

/// <summary>Used when no SMTP host is configured.</summary>
public sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        LogEmail(message.To, message.Subject);
        return Task.FromResult<string?>(null);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "DEV e-mail to {To}: {Subject}")]
    private partial void LogEmail(string to, string subject);
}

/// <summary>SMTP through MailKit (Mailpit in development).</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;
        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host ?? throw new InvalidOperationException("Notifications:Smtp:Host is not configured."), settings.Port, settings.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
        if (!string.IsNullOrEmpty(settings.UserName))
        {
            await client.AuthenticateAsync(settings.UserName, settings.Password ?? string.Empty, cancellationToken);
        }

        var response = await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
        return string.IsNullOrEmpty(response) ? mime.MessageId : response;
    }
}

/// <summary>Ruling D28: push goes through <see cref="IPushSender"/>; this dev implementation only logs. FCM comes later.</summary>
public sealed partial class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public Task<PushResult> SendAsync(PushMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        LogPush(message.Tokens.Count, message.Title);
        return Task.FromResult(new PushResult(null, []));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "DEV push to {DeviceCount} device(s): {Title}")]
    private partial void LogPush(int deviceCount, string title);
}
