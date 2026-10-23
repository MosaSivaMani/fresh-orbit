using System.Globalization;
using FreshOrbit.SharedKernel.Web.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace FreshOrbit.SharedKernel.Web.Concurrency;

/// <summary>
/// Optimistic concurrency over HTTP. Responses carry the aggregate row version as a strong ETag
/// (<c>"1234"</c>); updates quote it in <c>If-Match</c>. Missing → 428 VERSION_REQUIRED, unreadable → 400,
/// stale → 412 CONCURRENCY_CONFLICT (raised by <c>IUnitOfWork.SetExpectedVersion</c>).
/// </summary>
public static class IfMatch
{
    /// <summary>The quoted version; throws <see cref="VersionRequiredException"/> (428) when the header is missing.</summary>
    public static uint GetRequiredVersion(HttpRequest request)
        => TryGetVersion(request) ?? throw new VersionRequiredException();

    /// <summary>The quoted version, or null when the header is missing. An unreadable value is a 400.</summary>
    public static uint? TryGetVersion(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var raw = request.Headers.IfMatch.ToString().Trim();
        if (raw.Length == 0)
        {
            return null;
        }

        var value = raw.StartsWith("W/", StringComparison.Ordinal) ? raw[2..] : raw;
        value = value.Trim('"');
        return uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? version
            : throw new BadHttpRequestException("The If-Match header must be a version returned in an ETag.");
    }

    public static string FormatETag(uint version) => string.Create(CultureInfo.InvariantCulture, $"\"{version}\"");

    /// <summary>Sets the <c>ETag</c> response header to the version.</summary>
    public static void SetETag(this HttpResponse response, uint version)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers[HeaderNames.ETag] = FormatETag(version);
    }
}
