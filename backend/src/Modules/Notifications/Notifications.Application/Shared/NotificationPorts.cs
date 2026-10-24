namespace FreshOrbit.Notifications.Application.Shared;

/// <summary>Configuration section <c>Notifications</c>.</summary>
public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// Common template variables (NOT-R1) used only while the Settings business profile has not been saved; the
    /// profile's trade name and support contacts win once it exists.
    /// </summary>
    public string BusinessName { get; set; } = "Fresh Orbit";

    public string SupportPhone { get; set; } = string.Empty;

    public string SupportEmail { get; set; } = string.Empty;

    /// <summary>Messages sent per run of the <c>notifications.send</c> job (NOT-R6).</summary>
    public int BatchSize { get; set; } = 50;
}

/// <param name="Phone">E.164 phone number.</param>
/// <param name="TemplateCode">Our template code (e.g. <c>OTP_LOGIN</c>); the DLT sender maps it to the operator's DLT template id.</param>
/// <param name="Text">The rendered text; it must match the DLT-approved text with each variable filled in.</param>
/// <param name="Variables">The variable values in the order their placeholders appear in the template (the DLT-approved order).</param>
public sealed record SmsMessage(string Phone, string TemplateCode, string Text, IReadOnlyList<string> Variables);

public sealed record EmailMessage(string To, string Subject, string Body);

/// <param name="Tokens">FCM registration tokens of the user's devices.</param>
public sealed record PushMessage(IReadOnlyList<string> Tokens, string Title, string Body, IReadOnlyDictionary<string, string> Data);

/// <param name="InvalidTokens">Tokens the provider reported as no longer registered; they are removed.</param>
public sealed record PushResult(string? ProviderMessageId, IReadOnlyList<string> InvalidTokens);

/// <summary>
/// SMS adapter: a DLT-registered provider outside Development and Testing (refused at startup otherwise, SEC-1).
/// Implementations never log the text or the full phone number. Throws <see cref="SmsDeliveryException"/> when the
/// message cannot be sent (other exceptions are bugs).
/// </summary>
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
