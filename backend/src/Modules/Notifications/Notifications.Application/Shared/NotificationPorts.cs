namespace FreshOrbit.Notifications.Application.Shared;

/// <summary>Configuration section <c>Notifications</c>.</summary>
public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// Common template variables (NOT-R1). Read from configuration because Notifications may not use Settings.Public
    /// yet (see report); they should come from the business profile.
    /// </summary>
    public string BusinessName { get; set; } = "Fresh Orbit";

    public string SupportPhone { get; set; } = string.Empty;

    public string SupportEmail { get; set; } = string.Empty;

    /// <summary>Messages sent per run of the <c>notifications.send</c> job (NOT-R6).</summary>
    public int BatchSize { get; set; } = 50;
}

/// <param name="DltTemplateId">Operator DLT template id; required by real Indian SMS providers, ignored by the dev sender.</param>
public sealed record SmsMessage(string Phone, string Text, string? DltTemplateId);

public sealed record EmailMessage(string To, string Subject, string Body);

/// <param name="Tokens">FCM registration tokens of the user's devices.</param>
public sealed record PushMessage(IReadOnlyList<string> Tokens, string Title, string Body, IReadOnlyDictionary<string, string> Data);

/// <param name="InvalidTokens">Tokens the provider reported as no longer registered; they are removed.</param>
public sealed record PushResult(string? ProviderMessageId, IReadOnlyList<string> InvalidTokens);

/// <summary>SMS adapter (dev: logs the text; a DLT-registered provider later). Throws on a provider error.</summary>
public interface ISmsSender
{
    /// <returns>The provider's message id, when it gives one.</returns>
    Task<string?> SendAsync(SmsMessage message, CancellationToken cancellationToken);
}

/// <summary>E-mail adapter (SMTP via MailKit; Mailpit in dev). Throws on a provider error.</summary>
public interface IEmailSender
{
    Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Push adapter (Ruling D28: FCM later; dev no-op that logs). Throws on a provider error.</summary>
public interface IPushSender
{
    Task<PushResult> SendAsync(PushMessage message, CancellationToken cancellationToken);
}
