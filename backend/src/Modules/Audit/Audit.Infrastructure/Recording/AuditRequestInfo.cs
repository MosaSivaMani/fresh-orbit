using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace FreshOrbit.Audit.Infrastructure.Recording;

/// <summary>Request details stored on every audit entry.</summary>
internal static class AuditRequestInfo
{
    public static string? Ip(HttpContext? http) => http?.Connection.RemoteIpAddress?.ToString();

    public static string? CorrelationId(HttpContext? http) => Activity.Current?.TraceId.ToHexString() ?? http?.TraceIdentifier;
}
