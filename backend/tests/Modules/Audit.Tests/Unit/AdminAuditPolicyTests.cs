using AwesomeAssertions;
using FreshOrbit.Audit.Infrastructure.Recording;
using FreshOrbit.SharedKernel.Web.Auditing;
using Microsoft.AspNetCore.Http;

namespace FreshOrbit.Audit.Tests.Unit;

public sealed class AdminAuditPolicyTests
{
    [Theory]
    [InlineData("GET", "/api/v1/admin/roles")]
    [InlineData("POST", "/api/v1/me/addresses")]
    [InlineData("POST", "/api/v1/cart/items")]
    public void Reads_and_customer_writes_are_not_admin_writes(string method, string path)
        => AdminAuditPolicy.Decide(method, path, path, null).Mode.Should().Be(AdminAuditMode.NotAdminWrite);

    [Fact]
    public void Admin_write_without_a_named_action_derives_one_per_entity()
    {
        var decision = AdminAuditPolicy.Decide("PUT", "/api/v1/admin/categories/1", "/api/v1/admin/categories/{id:guid}", null);

        decision.Mode.Should().Be(AdminAuditMode.Audited);
        decision.Action.Should().BeNull();
    }

    [Fact]
    public void Named_action_and_exemption_come_from_the_central_tables()
    {
        AdminAuditPolicy.Decide("POST", "/api/v1/admin/refunds/x/approve", "/api/v1/admin/refunds/{refundId:guid}/approve", null).Action.Should().Be("REFUND_APPROVED");
        var login = AdminAuditPolicy.Decide("POST", "/api/v1/admin/auth/login", "/api/v1/admin/auth/login", null);
        login.Mode.Should().Be(AdminAuditMode.Exempt);
        login.ExemptReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Endpoint_metadata_wins_over_the_central_tables()
    {
        var named = new EndpointMetadataCollection(new AuditActionMetadata("REFUND_FAST_TRACKED"));
        AdminAuditPolicy.Decide("POST", "/api/v1/admin/refunds/x/approve", "/api/v1/admin/refunds/{refundId:guid}/approve", named).Action.Should().Be("REFUND_FAST_TRACKED");

        var exempt = new EndpointMetadataCollection(new AuditExemptMetadata("Test only."));
        AdminAuditPolicy.Decide("POST", "/api/v1/admin/x", "/api/v1/admin/x", exempt).Mode.Should().Be(AdminAuditMode.Exempt);
    }

    [Theory]
    [InlineData("PasswordHash", true)]
    [InlineData("TokenHash", true)]
    [InlineData("CodeHash", true)]
    [InlineData("GuestTokenHash", true)]
    [InlineData("PushToken", true)]
    [InlineData("WebhookSecret", true)]
    [InlineData("Payload", true)]
    [InlineData("Pincode", false)]
    [InlineData("Email", false)]
    [InlineData("Status", false)]
    [InlineData("PricePaise", false)]
    public void Sensitive_property_names_are_redacted(string name, bool sensitive)
        => AuditRedaction.IsSensitive(name).Should().Be(sensitive);
}
