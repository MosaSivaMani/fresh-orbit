using FreshOrbit.Identity.Public;
using FreshOrbit.Notifications.Application.Queueing;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Notifications.Domain.Messages;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.Notifications.Domain.Templates;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Notifications.Application.Sending;

/// <summary>NOT-R6: sends one batch of QUEUED SMS / EMAIL / PUSH messages.</summary>
public sealed class MessageDispatcher(
    INotificationsRepository repository,
    IIdentityApi identity,
    ISmsSender sms,
    IEmailSender email,
    IPushSender push,
    NotificationsOptions settings,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <returns>The number of messages handled (sent or failed).</returns>
    public Task<int> SendBatchAsync(CancellationToken cancellationToken)
        => repository.InTransactionAsync(
            async () =>
            {
                var batch = await repository.LockQueuedBatchAsync(settings.BatchSize, clock.UtcNow, cancellationToken);
                if (batch.Count == 0)
                {
                    return 0;
                }

                var users = (await identity.GetUsersAsync([.. batch.Select(m => m.UserId).Distinct()], cancellationToken)).ToDictionary(u => u.UserId);
                foreach (var message in batch)
                {
                    await SendAsync(message, users.GetValueOrDefault(message.UserId), cancellationToken);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                return batch.Count;
            },
            cancellationToken);

    private async Task SendAsync(Message message, UserSummary? user, CancellationToken cancellationToken)
    {
        try
        {
            switch (message.Channel)
            {
                case Channel.Sms when !string.IsNullOrEmpty(user?.Phone):
                    var template = (await repository.GetTemplatesAsync(message.TemplateCode, cancellationToken)).FirstOrDefault(t => t.Channel == Channel.Sms);
                    var variables = template is null ? null : TemplateText.ExtractValues(template.Body, message.Body);
                    message.MarkSent(await sms.SendAsync(new SmsMessage(user.Phone, message.TemplateCode, message.Body, variables ?? []), cancellationToken), clock.UtcNow);
                    break;
                case Channel.Email when !string.IsNullOrEmpty(user?.Email):
                    message.MarkSent(await email.SendAsync(new EmailMessage(user.Email, message.Subject ?? string.Empty, message.Body), cancellationToken), clock.UtcNow);
                    break;
                case Channel.Push:
                    var tokens = await repository.GetDeviceTokensAsync(message.UserId, cancellationToken);
                    if (tokens.Count == 0)
                    {
                        message.Fail(NotificationsErrors.NoRecipientAddress);
                        break;
                    }

                    var data = new Dictionary<string, string>(StringComparer.Ordinal) { ["templateCode"] = message.TemplateCode, ["messageId"] = message.Id.ToString() };
                    var result = await push.SendAsync(new PushMessage([.. tokens.Select(t => t.PushToken)], message.Subject ?? string.Empty, message.Body, data), cancellationToken);
                    foreach (var invalid in tokens.Where(t => result.InvalidTokens.Contains(t.PushToken, StringComparer.Ordinal)))
                    {
                        repository.Remove(invalid);
                    }

                    message.MarkSent(result.ProviderMessageId, clock.UtcNow);
                    break;
                default:
                    message.Fail(NotificationsErrors.NoRecipientAddress);
                    break;
            }
        }
        catch (SmsDeliveryException failure) when (failure.IsRetryable)
        {
            message.RecordFailure(failure.FailureCode, clock.UtcNow);
        }
        catch (SmsDeliveryException failure)
        {
            // E2E-1: a missing DLT template id or a provider refusal does not fix itself by retrying.
            message.Fail(failure.FailureCode);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not DomainException)
        {
            message.RecordFailure(exception.GetType().Name + ": " + exception.Message, clock.UtcNow);
        }
    }
}

/// <summary>Sends OTP SMS immediately for Identity (IOtpSender); nothing is stored (the code must not be).</summary>
public sealed class OtpSender(INotificationsRepository repository, NotificationComposer composer, ISmsSender sms) : IOtpSender
{
    public async Task SendAsync(OtpMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var code = message.Purpose == OtpPurpose.VerifyPhone ? "OTP_VERIFY_PHONE" : "OTP_LOGIN";
        var template = (await repository.GetTemplatesAsync(code, cancellationToken)).FirstOrDefault(t => t.Channel == Channel.Sms && t.IsActive)
            ?? throw new DomainException(NotificationsErrors.ConfigurationMissing, $"No active SMS template for {code}.");
        var values = await composer.ValuesAsync(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["code"] = message.Code,
                ["expiresInMinutes"] = message.ExpiresInMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            cancellationToken);
        // E2E-1: a provider failure surfaces as SmsDeliveryException (503 SMS_PROVIDER_UNAVAILABLE), never as a 500.
        await sms.SendAsync(new SmsMessage(message.Phone, code, TemplateText.Render(template.Body, values), TemplateText.OrderedValues(template.Body, values)), cancellationToken);
    }
}
