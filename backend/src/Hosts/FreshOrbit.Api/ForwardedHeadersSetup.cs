using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Api;

/// <summary>
/// Configuration section <c>ForwardedHeaders</c> (SEC-2). <see cref="KnownNetworks"/> lists the reverse proxies whose
/// <c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c> are believed, as CIDR (<c>172.18.0.0/16</c>) or single
/// addresses (<c>10.0.0.5</c>). Empty (the default) trusts nobody, not even loopback.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public string[] KnownNetworks { get; set; } = [];

    /// <summary>Parses one entry; false when it is neither a CIDR network nor an IP address.</summary>
    public static bool TryParseNetwork(string? value, out System.Net.IPNetwork network)
    {
        network = default;
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.Contains('/', StringComparison.Ordinal))
        {
            return System.Net.IPNetwork.TryParse(text, out network);
        }

        if (!IPAddress.TryParse(text, out var address))
        {
            return false;
        }

        network = new System.Net.IPNetwork(address, address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
        return true;
    }
}

public static class ForwardedHeadersSetup
{
    /// <summary>
    /// <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> are applied only when the direct peer is in
    /// <c>ForwardedHeaders:KnownNetworks</c>, and only one hop is taken (Caddy sits directly in front). The framework
    /// defaults (loopback trusted) are cleared. Invalid entries stop the host at startup (rule 7).
    /// </summary>
    public static IServiceCollection AddFreshOrbitForwardedHeaders(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersSettings>()
            .BindConfiguration(ForwardedHeadersSettings.SectionName)
            .Validate(s => s.KnownNetworks.All(n => ForwardedHeadersSettings.TryParseNetwork(n, out _)), "Every ForwardedHeaders:KnownNetworks entry must be a CIDR network or an IP address.")
            .ValidateOnStart();
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ForwardedHeadersSettings>>((options, settings) =>
            {
                options.ForwardLimit = 1;
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
                foreach (var entry in settings.Value.KnownNetworks)
                {
                    if (ForwardedHeadersSettings.TryParseNetwork(entry, out var network))
                    {
                        options.KnownIPNetworks.Add(network);
                    }
                }

                // With no known proxy at all the middleware would believe every peer, so nothing is forwarded then.
                options.ForwardedHeaders = options.KnownIPNetworks.Count == 0
                    ? ForwardedHeaders.None
                    : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });
        return services;
    }
}
