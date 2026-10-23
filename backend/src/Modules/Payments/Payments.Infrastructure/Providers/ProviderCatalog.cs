using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Providers;
using FreshOrbit.Payments.Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Payments.Infrastructure.Providers;

/// <summary>Settings of one adapter, section <c>Payments:Providers:&lt;KEY&gt;</c>.</summary>
public sealed class ProviderSettings
{
    public bool Enabled { get; set; }

    /// <summary>Public checkout key id sent to clients.</summary>
    public string? KeyId { get; set; }

    /// <summary>Webhook HMAC secret (a secret: environment variable outside Development).</summary>
    public string? WebhookSecret { get; set; }
}

/// <summary>Section <c>Payments:Providers</c>: settings per adapter key.</summary>
public sealed class PaymentProvidersOptions
{
    public const string SectionName = "Payments:Providers";

    public Dictionary<string, ProviderSettings> Adapters { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ProviderSettings? For(string key) => Adapters.TryGetValue(key, out var settings) ? settings : null;

    public bool IsEnabled(string key) => For(key)?.Enabled == true;
}

/// <summary>Refuses to start with the Fake gateway enabled in Production (PAY-R2).</summary>
internal sealed class PaymentProvidersOptionsValidator(IServiceProvider services) : IValidateOptions<PaymentProvidersOptions>
{
    public ValidateOptionsResult Validate(string? name, PaymentProvidersOptions options)
    {
        var environment = services.GetService<IHostEnvironment>();
        if (environment is not null && environment.IsProduction() && options.IsEnabled(PaymentProviders.Fake))
        {
            return ValidateOptionsResult.Fail("Payments:Providers:FAKE must not be enabled in Production.");
        }

        var fake = options.For(PaymentProviders.Fake);
        if (fake?.Enabled == true && string.IsNullOrWhiteSpace(fake.WebhookSecret))
        {
            return ValidateOptionsResult.Fail("Payments:Providers:FAKE:WebhookSecret is required when the Fake gateway is enabled.");
        }

        return ValidateOptionsResult.Success;
    }
}

internal sealed class PaymentProviderCatalog(IEnumerable<IPaymentProvider> adapters, IOptionsMonitor<PaymentProvidersOptions> providerOptions, IOptionsMonitor<PaymentsOptions> paymentsOptions)
    : IPaymentProviderCatalog
{
    private readonly Dictionary<string, IPaymentProvider> _adapters = adapters.ToDictionary(a => a.Key, StringComparer.OrdinalIgnoreCase);

    public IPaymentProvider? Find(string key) => _adapters.TryGetValue(key, out var adapter) ? adapter : null;

    public IPaymentProvider? FindEnabled(string key)
        => Find(key) is { } adapter && providerOptions.CurrentValue.IsEnabled(adapter.Key) ? adapter : null;

    public IPaymentProvider GetOnlineProvider()
    {
        var key = paymentsOptions.CurrentValue.OnlineProvider;
        return string.IsNullOrWhiteSpace(key)
            ? throw new PaymentProviderUnavailableException("Online payments are not configured.")
            : FindEnabled(key) is { } adapter && adapter.Capabilities.HasFlag(ProviderCapabilities.OnlineCheckout)
                ? adapter
                : throw new PaymentProviderUnavailableException("The online payment provider is not enabled.");
    }

    public ProviderCapabilities CapabilitiesOf(string key) => Find(key)?.Capabilities ?? ProviderCapabilities.None;
}

/// <summary>Cash and UPI on delivery: collected by the rider; nothing else is supported (PAY-R2).</summary>
internal abstract class CollectedOnDeliveryProvider : IPaymentProvider
{
    public abstract string Key { get; }

    public ProviderCapabilities Capabilities => ProviderCapabilities.CollectedOnDelivery;

    public Task<ProviderOrder> CreateOrderAsync(CreateProviderOrder request, CancellationToken cancellationToken) => throw NotSupported();

    public bool VerifySignature(IReadOnlyDictionary<string, string> headers, ReadOnlySpan<byte> rawBody) => throw NotSupported();

    public ProviderWebhook ParseWebhook(IReadOnlyDictionary<string, string> headers, ReadOnlySpan<byte> rawBody) => throw NotSupported();

    public Task<ProviderPaymentState> GetPaymentAsync(string providerOrderId, CancellationToken cancellationToken) => throw NotSupported();

    public Task<ProviderRefundResult> CreateRefundAsync(ProviderRefundRequest request, CancellationToken cancellationToken) => throw NotSupported();

    public Task<ProviderRefundState> GetRefundAsync(string providerRefundId, CancellationToken cancellationToken) => throw NotSupported();

    private NotSupportedException NotSupported() => new($"{Key} payments are collected on delivery; check Capabilities before calling the adapter.");
}

internal sealed class CashOnDeliveryProvider : CollectedOnDeliveryProvider
{
    public override string Key => PaymentProviders.Cash;
}

internal sealed class UpiOnDeliveryProvider : CollectedOnDeliveryProvider
{
    public override string Key => PaymentProviders.UpiOnDelivery;
}
