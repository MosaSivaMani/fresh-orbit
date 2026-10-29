using System.Net;
using System.Security.Claims;
using AwesomeAssertions;
using FreshOrbit.Api;
using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.SharedKernel.Web.Auth;

namespace FreshOrbit.Identity.Tests.Unit;

/// <summary>ME-8 / MB-5: rate-limit buckets for customers behind one carrier NAT address.</summary>
public sealed class RateLimitPartitioningTests
{
    private static readonly IPAddress SharedIp = IPAddress.Parse("100.64.0.1");
    private static readonly RateLimitRule Rule = new() { PermitLimit = 600, PerUserPermitLimit = 120, WindowSeconds = 60 };

    [Fact]
    public void Signed_in_callers_behind_one_ip_each_get_their_own_bucket_with_the_per_user_limit()
    {
        var first = RateLimitPartitioning.ForUserOrIp("search", Rule, SignedIn("user-1"), SharedIp);
        var second = RateLimitPartitioning.ForUserOrIp("search", Rule, SignedIn("user-2"), SharedIp);

        first.PermitLimit.Should().Be(120);
        second.PermitLimit.Should().Be(120);
        first.PartitionKey.Should().NotBe(second.PartitionKey);
        first.PartitionKey.Should().NotContain(SharedIp.ToString());
    }

    [Fact]
    public void Anonymous_callers_share_the_ip_bucket_with_the_per_ip_ceiling()
    {
        var first = RateLimitPartitioning.ForUserOrIp("search", Rule, new ClaimsPrincipal(new ClaimsIdentity()), SharedIp);
        var second = RateLimitPartitioning.ForUserOrIp("search", Rule, null, SharedIp);

        first.Should().Be(second);
        first.PermitLimit.Should().Be(600);
        first.PartitionKey.Should().Contain(SharedIp.ToString());
    }

    [Fact]
    public void A_subject_claim_on_an_unauthenticated_identity_is_not_trusted()
    {
        var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity([new Claim(FreshOrbitClaimTypes.Subject, "user-1")]));

        RateLimitPartitioning.ForUserOrIp("search", Rule, unauthenticated, SharedIp).PermitLimit.Should().Be(600);
    }

    [Fact]
    public void A_rule_without_a_per_user_limit_counts_signed_in_callers_per_ip()
    {
        var ipOnly = new RateLimitRule { PermitLimit = 300, WindowSeconds = 3600 };

        var budget = RateLimitPartitioning.ForUserOrIp("otp-request", ipOnly, SignedIn("user-1"), SharedIp);

        budget.PermitLimit.Should().Be(300);
        budget.PartitionKey.Should().Contain(SharedIp.ToString());
    }

    [Fact]
    public void Policies_are_partitioned_by_name_so_buckets_never_mix()
        => RateLimitPartitioning.ForUserOrIp("search", Rule, null, SharedIp).PartitionKey
            .Should().NotBe(RateLimitPartitioning.ForUserOrIp("public", Rule, null, SharedIp).PartitionKey);

    [Fact]
    public void Default_ceilings_allow_many_customers_behind_one_ip_and_keep_the_per_phone_guard()
    {
        var limits = new RateLimitingOptions();
        var identity = new IdentitySettings();

        limits.OtpRequest.PermitLimit.Should().Be(300);
        limits.OtpRequest.WindowSeconds.Should().Be(3600);
        limits.Search.PermitLimit.Should().Be(600);
        limits.Search.PerUserPermitLimit.Should().Be(120);
        limits.Public.PermitLimit.Should().Be(1200);
        limits.Public.PerUserPermitLimit.Should().Be(240);
        identity.OtpPerIpPerHour.Should().Be(300);
        identity.OtpPerPhonePer10Minutes.Should().Be(3);
        identity.OtpPerPhonePerDay.Should().Be(10);
    }

    private static ClaimsPrincipal SignedIn(string subject)
        => new(new ClaimsIdentity([new Claim(FreshOrbitClaimTypes.Subject, subject)], "Bearer"));
}
