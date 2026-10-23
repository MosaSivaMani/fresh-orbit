using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Notifications.Domain.Messages;

/// <summary>Stored as QUEUED / SENT / FAILED / READ.</summary>
public enum MessageStatus
{
    Queued,
    Sent,
    Failed,
    Read,
}

/// <summary>One rendered message on one channel (NOT-R4–R6, R9).</summary>
public sealed class Message : AggregateRoot
{
    private Message()
    {
        TemplateCode = Body = string.Empty;
        Payload = new Dictionary<string, string>();
    }

    public Guid UserId { get; private set; }

    public Channel Channel { get; private set; }

    public string TemplateCode { get; private set; }

    public string? Subject { get; private set; }

    public string Body { get; private set; }

    /// <summary>The data the message was rendered with.</summary>
    public Dictionary<string, string> Payload { get; private set; }

    public MessageStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public string? LastError { get; private set; }

    /// <summary><c>key:channel:user</c>; unique when not null (NOT-R4).</summary>
    public string? DedupeKey { get; private set; }

    public DateTimeOffset QueuedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary>After a provider error the message waits (1, 2, 3, 4 minutes) before the next attempt.</summary>
    public DateTimeOffset? NextAttemptAt { get; private set; }

    public static string ComposeDedupeKey(string key, Channel channel, Guid userId)
        => FormattableString.Invariant($"{key}:{channel.ToCode()}:{userId}");

    /// <summary>IN_APP messages are delivered by being stored, so they start SENT.</summary>
    public static Message Queue(Guid userId, Channel channel, string templateCode, string? subject, string body, IReadOnlyDictionary<string, string> payload, string? dedupeKey, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var inApp = channel == Channel.InApp;
        return new Message
        {
            UserId = userId,
            Channel = channel,
            TemplateCode = templateCode,
            Subject = subject,
            Body = body,
            Payload = new Dictionary<string, string>(payload, StringComparer.Ordinal),
            Status = inApp ? MessageStatus.Sent : MessageStatus.Queued,
            DedupeKey = dedupeKey is null ? null : ComposeDedupeKey(dedupeKey, channel, userId),
            QueuedAt = now,
            SentAt = inApp ? now : null,
        };
    }

    public void MarkSent(string? providerMessageId, DateTimeOffset now)
    {
        if (Status != MessageStatus.Queued)
        {
            return;
        }

        Attempts++;
        Status = MessageStatus.Sent;
        ProviderMessageId = providerMessageId;
        LastError = null;
        NextAttemptAt = null;
        SentAt = now;
    }

    /// <summary>A provider error: retried on the next run until <see cref="NotificationsErrors.MaxAttempts"/>.</summary>
    public void RecordFailure(string error, DateTimeOffset now)
    {
        if (Status != MessageStatus.Queued)
        {
            return;
        }

        Attempts++;
        LastError = Truncate(error);
        if (Attempts >= NotificationsErrors.MaxAttempts)
        {
            Status = MessageStatus.Failed;
            NextAttemptAt = null;
        }
        else
        {
            NextAttemptAt = now.AddMinutes(Attempts);
        }
    }

    /// <summary>A failure that retrying cannot fix (e.g. NO_RECIPIENT_ADDRESS).</summary>
    public void Fail(string error)
    {
        if (Status != MessageStatus.Queued)
        {
            return;
        }

        Attempts++;
        LastError = Truncate(error);
        Status = MessageStatus.Failed;
    }

    /// <summary>Idempotent; only a SENT in-app message becomes READ.</summary>
    public bool MarkRead(DateTimeOffset now)
    {
        if (Channel != Channel.InApp || Status != MessageStatus.Sent)
        {
            return false;
        }

        Status = MessageStatus.Read;
        ReadAt = now;
        return true;
    }

    private static string Truncate(string error) => error.Length <= 500 ? error : error[..500];
}
