using FreshOrbit.Audit.Public;
using FreshOrbit.Identity.Public;
using FreshOrbit.Notifications.Application.Queueing;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.Notifications.Domain.Templates;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Notifications.Application.Templates;

public sealed record TemplateResponse(
    Guid TemplateId,
    string Code,
    string Channel,
    string? Category,
    string? Subject,
    string Body,
    bool IsActive,
    IReadOnlyList<string> Variables,
    string? DltTemplateId,
    string Version);

public sealed record UpdateTemplateRequest(string? Subject, string Body, bool IsActive);

/// <param name="Sent">False when the staff member has no address for the channel (e.g. PUSH / IN_APP).</param>
public sealed record TestSendResponse(bool Sent, string Channel, string? Recipient, string? Subject, string Body);

/// <summary>Staff template administration (NOT-R1, NOT-R10).</summary>
public sealed class TemplateAdminService(
    INotificationsRepository repository,
    NotificationComposer composer,
    IIdentityApi identity,
    ISmsSender sms,
    IEmailSender email,
    ICurrentActor actor,
    IUnitOfWork unitOfWork,
    IAuditApi audit)
{
    public async Task<IReadOnlyList<TemplateResponse>> ListAsync(CancellationToken cancellationToken)
        => [.. (await repository.GetAllTemplatesAsync(cancellationToken)).Select(ToResponse)];

    public async Task<TemplateResponse> UpdateAsync(string code, string channel, UpdateTemplateRequest request, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var template = await FindAsync(code, channel, cancellationToken);
        unitOfWork.SetExpectedVersion(template, expectedVersion);
        var before = ToResponse(template);
        template.Edit(request.Subject, request.Body, request.IsActive);
        audit.Record(new AuditRecord("NOTIFICATION_TEMPLATE_CHANGED", "NOTIFICATION_TEMPLATE", $"{template.Code}/{channel}", before, ToResponse(template)));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(template);
    }

    /// <summary>NOT-R10: sample values, sent to the caller now, nothing stored.</summary>
    public async Task<TestSendResponse> TestSendAsync(string code, string channel, CancellationToken cancellationToken)
    {
        var template = await FindAsync(code, channel, cancellationToken);
        var user = actor.UserId is { } userId ? await identity.GetUserAsync(userId, cancellationToken) : null;
        var values = await composer.ValuesAsync(TemplateText.SampleData(template.Variables), cancellationToken);
        var subject = template.Subject is null ? null : TemplateText.Render(template.Subject, values);
        var body = TemplateText.Render(template.Body, values);
        var channelCode = template.Channel.ToCode();
        switch (template.Channel)
        {
            case Channel.Email when !string.IsNullOrEmpty(user?.Email):
                await email.SendAsync(new EmailMessage(user.Email, "[TEST] " + subject, body), cancellationToken);
                return new TestSendResponse(true, channelCode, user.Email, subject, body);
            case Channel.Sms when !string.IsNullOrEmpty(user?.Phone):
                await sms.SendAsync(new SmsMessage(user.Phone, template.Code, body, TemplateText.OrderedValues(template.Body, values)), cancellationToken);
                return new TestSendResponse(true, channelCode, user.Phone, subject, body);
            default:
                return new TestSendResponse(false, channelCode, null, subject, body);
        }
    }

    private async Task<Template> FindAsync(string code, string channel, CancellationToken cancellationToken)
    {
        var template = EnumCodes.TryToEnum<Channel>(channel, out var parsed)
            ? (await repository.GetTemplatesAsync(code, cancellationToken)).FirstOrDefault(t => t.Channel == parsed)
            : null;
        return template ?? throw new NotFoundException("The template does not exist.");
    }

    public static TemplateResponse ToResponse(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return new TemplateResponse(
            template.Id,
            template.Code,
            template.Channel.ToCode(),
            template.Category?.ToCode(),
            template.Subject,
            template.Body,
            template.IsActive,
            [.. template.Variables, .. Template.CommonVariables],
            template.DltTemplateId,
            template.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
