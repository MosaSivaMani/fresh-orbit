using FluentValidation;
using FreshOrbit.Notifications.Domain.Devices;
using FreshOrbit.Notifications.Domain.Messages;
using FreshOrbit.Notifications.Domain.Preferences;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;
using FreshOrbit.SharedKernel.Time;

namespace FreshOrbit.Notifications.Application.Inbox;

public sealed record InboxRequest(bool UnreadOnly = false, int Page = 1, int PageSize = 20);

public sealed record InboxItem(Guid MessageId, string TemplateCode, string? Title, string Body, bool IsRead, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

public sealed record InboxResponse(IReadOnlyList<InboxItem> Items, int Page, int PageSize, int TotalCount, int UnreadCount);

/// <summary>Body of <c>PUT /me/device-tokens</c> (Ruling D28).</summary>
public sealed record RegisterDeviceTokenRequest(string PushToken, string Provider, string Platform);

public sealed record DeviceTokenResponse(string PushToken, string Provider, string Platform, DateTimeOffset LastSeenAt);

public sealed record PreferenceItem(string Category, string Channel, bool Enabled);

public sealed record PreferencesResponse(IReadOnlyList<PreferenceItem> Preferences);

public sealed record ReplacePreferencesRequest(IReadOnlyList<PreferenceItem> Preferences);

public sealed class InboxRequestValidator : AbstractValidator<InboxRequest>
{
    public InboxRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrorCode("PAGE_OUT_OF_RANGE");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithErrorCode("PAGE_SIZE_OUT_OF_RANGE");
    }
}

public sealed class RegisterDeviceTokenRequestValidator : AbstractValidator<RegisterDeviceTokenRequest>
{
    public RegisterDeviceTokenRequestValidator()
    {
        RuleFor(x => x.PushToken).NotEmpty().MaximumLength(DeviceToken.MaxTokenLength);
        RuleFor(x => x.Provider).Must(p => EnumCodes.IsCode<PushProvider>(p)).WithErrorCode("VALUE_INVALID").WithMessage("The provider must be FCM.");
        RuleFor(x => x.Platform).Must(p => EnumCodes.IsCode<DevicePlatform>(p)).WithErrorCode("VALUE_INVALID")
            .WithMessage("The platform must be ANDROID, IOS or WEB.");
    }
}

public sealed class ReplacePreferencesRequestValidator : AbstractValidator<ReplacePreferencesRequest>
{
    public ReplacePreferencesRequestValidator()
    {
        RuleFor(x => x.Preferences).NotNull();
        RuleForEach(x => x.Preferences).ChildRules(item =>
        {
            item.RuleFor(p => p.Category).Must(c => EnumCodes.IsCode<Category>(c)).WithErrorCode("VALUE_INVALID")
                .WithMessage("The category must be ORDER_UPDATES or OFFERS.");
            item.RuleFor(p => p.Channel).Must(c => EnumCodes.IsCode<Channel>(c)).WithErrorCode("VALUE_INVALID")
                .WithMessage("The channel must be SMS, EMAIL, PUSH or IN_APP.");
        });
        RuleFor(x => x.Preferences).Must(p => p is null || p.Select(i => (i.Category, i.Channel)).Distinct().Count() == p.Count)
            .WithErrorCode("DUPLICATE_VALUE").WithMessage("Each category and channel may appear once.");
    }
}

/// <summary>The calling user's in-app messages, device tokens and preferences (NOT-R7–R9).</summary>
public sealed class InboxService(INotificationsRepository repository, ICurrentActor actor, IUnitOfWork unitOfWork, IClock clock)
{
    private Guid UserId => actor.UserId ?? throw new NotFoundException("The caller has no user account.");

    public async Task<InboxResponse> ListAsync(InboxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (items, total) = await repository.GetInAppAsync(UserId, request.UnreadOnly, (request.Page - 1) * request.PageSize, request.PageSize, cancellationToken);
        var unread = request.UnreadOnly ? total : (await repository.GetInAppAsync(UserId, true, 0, 0, cancellationToken)).TotalCount;
        return new InboxResponse(
            [.. items.Select(m => new InboxItem(m.Id, m.TemplateCode, m.Subject, m.Body, m.Status == MessageStatus.Read, m.QueuedAt, m.ReadAt))],
            request.Page,
            request.PageSize,
            total,
            unread);
    }

    public async Task MarkReadAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await repository.GetMessageAsync(UserId, messageId, cancellationToken);
        if (message is null || message.Channel != Channel.InApp)
        {
            throw new NotFoundException("The message does not exist.");
        }

        if (message.MarkRead(clock.UtcNow))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<int> MarkAllReadAsync(CancellationToken cancellationToken) => repository.MarkAllReadAsync(UserId, clock.UtcNow, cancellationToken);

    public async Task<DeviceTokenResponse> RegisterDeviceAsync(RegisterDeviceTokenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var provider = request.Provider.ToEnum<PushProvider>();
        var platform = request.Platform.ToEnum<DevicePlatform>();
        var token = await repository.FindDeviceTokenAsync(request.PushToken, cancellationToken);
        if (token is null)
        {
            token = DeviceToken.Register(UserId, request.PushToken, provider, platform, clock.UtcNow);
            repository.Add(token);
        }
        else
        {
            token.Refresh(UserId, provider, platform, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DeviceTokenResponse(token.PushToken, token.Provider.ToCode(), token.Platform.ToCode(), token.LastSeenAt);
    }

    /// <summary>Removes only the caller's token; an unknown token is a no-op.</summary>
    public async Task UnregisterDeviceAsync(string pushToken, CancellationToken cancellationToken)
    {
        var token = await repository.FindDeviceTokenAsync(pushToken, cancellationToken);
        if (token is not null && token.UserId == UserId)
        {
            repository.Remove(token);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Every category × channel, stored value or default.</summary>
    public async Task<PreferencesResponse> GetPreferencesAsync(CancellationToken cancellationToken)
    {
        var stored = await repository.GetPreferencesAsync(UserId, cancellationToken);
        var items = new List<PreferenceItem>();
        foreach (var category in Enum.GetValues<Category>())
        {
            foreach (var channel in Enum.GetValues<Channel>())
            {
                var row = stored.FirstOrDefault(p => p.Category == category && p.Channel == channel);
                var enabled = category == Category.OrderUpdates && channel == Channel.Sms || (row?.Enabled ?? Preference.DefaultFor(category));
                items.Add(new PreferenceItem(category.ToCode(), channel.ToCode(), enabled));
            }
        }

        return new PreferencesResponse(items);
    }

    /// <summary>NOT-R8: replaces the full set; pairs not sent fall back to their default.</summary>
    public async Task<PreferencesResponse> ReplacePreferencesAsync(ReplacePreferencesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var wanted = request.Preferences.ToDictionary(p => (p.Category.ToEnum<Category>(), p.Channel.ToEnum<Channel>()), p => p.Enabled);
        foreach (var ((category, channel), enabled) in wanted)
        {
            Preference.EnsureAllowed(category, channel, enabled);
        }

        var stored = await repository.GetPreferencesAsync(UserId, cancellationToken);
        foreach (var category in Enum.GetValues<Category>())
        {
            foreach (var channel in Enum.GetValues<Channel>())
            {
                var enabled = wanted.TryGetValue((category, channel), out var value) ? value : Preference.DefaultFor(category);
                var row = stored.FirstOrDefault(p => p.Category == category && p.Channel == channel);
                if (row is null)
                {
                    repository.Add(Preference.Create(UserId, category, channel, enabled));
                }
                else
                {
                    row.Set(enabled);
                }
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetPreferencesAsync(cancellationToken);
    }
}
