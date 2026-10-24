using AwesomeAssertions;
using FreshOrbit.Audit.Infrastructure.Recording;
using FreshOrbit.E1Testing;
using FreshOrbit.SharedKernel.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Audit.Tests.Integration;

/// <summary>
/// SEC-7 / AUD-R6: every mapped <c>/api/v1/admin</c> POST, PUT, PATCH and DELETE of the real Api host is either
/// audited by <see cref="AdminWriteAuditInterceptor"/> or exempt with a reason, and the exemptions are only session
/// housekeeping. An audited admin write must require an authenticated caller, or no staff actor would be recorded.
/// </summary>
public sealed class AdminWriteCoverageTests(E1Database database) : E1ApiTestBase(database)
{
    private static readonly string[] AllowedExemptions =
    [
        "POST /api/v1/admin/auth/login",
        "POST /api/v1/admin/auth/logout",
    ];

    [Fact]
    public void Every_admin_write_endpoint_is_audited_or_exempt_with_a_reason()
    {
        var writes = AdminWrites();
        writes.Should().HaveCountGreaterThan(50, "the Api maps the admin endpoints of every module");

        var decisions = writes.Select(w => (w.Key, w.Endpoint, Decision: AdminAuditPolicy.Decide(w.Method, w.Pattern, w.Pattern, w.Endpoint.Metadata))).ToList();

        decisions.Should().OnlyContain(d => d.Decision.Mode != AdminAuditMode.NotAdminWrite);
        decisions.Where(d => d.Decision.Mode == AdminAuditMode.Exempt).Select(d => d.Key)
            .Should().BeSubsetOf(AllowedExemptions, "only login and logout may skip the audit trail");
        decisions.Where(d => d.Decision.Mode == AdminAuditMode.Exempt)
            .Should().OnlyContain(d => !string.IsNullOrWhiteSpace(d.Decision.ExemptReason));
        decisions.Where(d => d.Decision.Mode == AdminAuditMode.Audited && !RequiresAuthentication(d.Endpoint)).Select(d => d.Key)
            .Should().BeEmpty("an audited admin write needs a signed-in staff actor to record");
    }

    [Fact]
    public void Named_actions_and_exemptions_match_mapped_endpoints()
    {
        var keys = AdminWrites().Select(w => w.Key).ToHashSet(StringComparer.Ordinal);

        AdminAuditPolicy.Actions.Keys.Should().BeSubsetOf(keys, "a renamed route must not silently lose its audit action");
        AdminAuditPolicy.Exemptions.Keys.Should().BeSubsetOf(keys);
        AdminAuditPolicy.Exemptions.Keys.Should().BeSubsetOf(AllowedExemptions);
        AdminAuditPolicy.Actions.Values.Should().OnlyContain(action => ErrorCodes.IsUpperSnake(action));
    }

    private List<(string Key, string Method, string Pattern, RouteEndpoint Endpoint)> AdminWrites()
    {
        var sources = Factory.Services.GetServices<EndpointDataSource>();
        return [.. sources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith(AdminAuditPolicy.AdminPrefix, StringComparison.OrdinalIgnoreCase) == true)
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []).Select(m => (Method: m, Endpoint: e)))
            .Where(x => AdminAuditPolicy.IsWriteMethod(x.Method))
            .Select(x => (AdminAuditPolicy.Key(x.Method, x.Endpoint.RoutePattern.RawText!), x.Method, x.Endpoint.RoutePattern.RawText!, x.Endpoint))
            .DistinctBy(x => x.Item1)];
    }

    private static bool RequiresAuthentication(Endpoint endpoint)
        => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
            && (endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0 || endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>().Count > 0);
}
