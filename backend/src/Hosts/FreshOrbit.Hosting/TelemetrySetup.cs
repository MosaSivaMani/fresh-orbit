using System.Diagnostics.CodeAnalysis;
using FreshOrbit.Hosting.Telemetry;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FreshOrbit.Hosting;

/// <summary>
/// OpenTelemetry metrics and traces (BE-8, D332). The one switch is <see cref="OtlpEndpointKey"/>:
/// <list type="bullet">
/// <item>Empty or absent: telemetry is OFF by explicit configuration. No OpenTelemetry provider, exporter or sampler is
/// registered, so the host pays nothing (the 512 MB demo runs this way). This is a documented state, not a fallback.</item>
/// <item>An absolute http(s) URL of an OTLP/HTTP receiver (for example <c>http://alloy:4318</c>): traces and metrics are
/// exported there with OTLP over HTTP/protobuf (<c>/v1/traces</c>, <c>/v1/metrics</c>).</item>
/// <item>Anything else refuses start-up (rule 7).</item>
/// </list>
/// Traces: ASP.NET Core requests (health probes excluded), outgoing HttpClient calls, Npgsql commands (the SQL behind
/// EF Core; Npgsql emits these itself) and recurring job runs. Metrics: ASP.NET Core, Kestrel, HttpClient, the .NET
/// runtime, the Npgsql pool and <see cref="FreshOrbitMetrics"/>.
/// </summary>
public static class TelemetrySetup
{
    public const string SectionName = "Telemetry";

    public const string OtlpEndpointKey = "Telemetry:OtlpEndpoint";

    /// <summary>The ActivitySource and Meter Npgsql publishes (no extra package needed).</summary>
    public const string NpgsqlSourceName = "Npgsql";

    /// <summary>True when <see cref="OtlpEndpointKey"/> holds a value (validity is checked at start-up).</summary>
    public static bool IsEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return !string.IsNullOrWhiteSpace(configuration[OtlpEndpointKey]);
    }

    /// <summary>
    /// Registers <see cref="FreshOrbitMetrics"/> always, and OpenTelemetry with the OTLP exporters only when
    /// <see cref="OtlpEndpointKey"/> is set. <paramref name="serviceName"/> names the host in the backend
    /// (<c>fresh-orbit-api</c>, <c>fresh-orbit-worker</c>); <paramref name="web"/> adds the ASP.NET Core instrumentation.
    /// </summary>
    public static IServiceCollection AddFreshOrbitTelemetry(this IServiceCollection services, IConfiguration configuration, string serviceName, bool web)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        services.AddMetrics();
        services.TryAddSingleton<FreshOrbitMetrics>();
        services.AddOptions<TelemetryOptions>().BindConfiguration(SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TelemetryOptions>, TelemetryOptionsValidator>());

        if (!TelemetryOptions.TryParseEndpoint(configuration[OtlpEndpointKey], out var parsed))
        {
            // Off (no endpoint), or an invalid endpoint that the validator refuses at start-up.
            return services;
        }

        Uri endpoint = parsed;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: typeof(TelemetrySetup).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing =>
            {
                if (web)
                {
                    tracing.AddAspNetCoreInstrumentation(options => options.Filter = context => !IsHealthProbe(context));
                }

                tracing
                    .AddHttpClientInstrumentation()
                    .AddSource(NpgsqlSourceName)
                    .AddSource(FreshOrbitMetrics.ActivitySourceName)
                    .AddOtlpExporter(options => Configure(options, endpoint, "v1/traces"));
            })
            .WithMetrics(metrics =>
            {
                if (web)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                metrics
                    .AddHttpClientInstrumentation()
                    .AddMeter("System.Runtime", NpgsqlSourceName, FreshOrbitMetrics.MeterName)
                    .AddOtlpExporter(options => Configure(options, endpoint, "v1/metrics"));
            });
        return services;
    }

    private static bool IsHealthProbe(HttpContext context) => context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

    private static void Configure(OtlpExporterOptions options, Uri endpoint, string signalPath)
    {
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.Endpoint = new Uri(endpoint, signalPath);
    }
}

/// <summary>Configuration section <c>Telemetry</c> (D332).</summary>
public sealed class TelemetryOptions
{
    /// <summary>Base URL of the OTLP/HTTP receiver; empty = telemetry off.</summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>An absolute http or https URL, normalised to end with a slash so signal paths append to it.</summary>
    public static bool TryParseEndpoint(string? value, [NotNullWhen(true)] out Uri? endpoint)
    {
        endpoint = null;
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        endpoint = parsed.AbsoluteUri.EndsWith('/') ? parsed : new Uri(parsed.AbsoluteUri + "/");
        return true;
    }
}

internal sealed class TelemetryOptionsValidator : IValidateOptions<TelemetryOptions>
{
    public ValidateOptionsResult Validate(string? name, TelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.OtlpEndpoint) || TelemetryOptions.TryParseEndpoint(options.OtlpEndpoint, out _))
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail("Telemetry:OtlpEndpoint must be an absolute http or https URL of an OTLP/HTTP receiver, or empty to turn telemetry off.");
    }
}
