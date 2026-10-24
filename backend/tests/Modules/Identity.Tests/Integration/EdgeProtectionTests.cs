using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.Api;
using FreshOrbit.E1Testing;
using FreshOrbit.Identity.Domain.Otp;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Identity.Tests.Integration;

/// <summary>
/// SEC-2 / SEC-3: forwarded headers are believed only from <c>ForwardedHeaders:KnownNetworks</c>, and every rate-limit
/// policy partitions by the real client (IP) or user. The direct peer address is set per request with
/// <see cref="EdgeApiFactory.PeerHeader"/>, as the TCP peer would be in production.
/// </summary>
public sealed class EdgeProtectionTests(E1Database database) : IAsyncLifetime
{
    private const string Proxy = "10.20.0.5";
    private const string ProxyNetwork = "10.20.0.0/16";
    private const string CouponPath = "/api/v1/cart/coupon";

    private readonly List<EdgeApiFactory> _factories = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factories)
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Forwarded_for_is_ignored_when_no_network_is_trusted()
    {
        var factory = Create(new Dictionary<string, string?>());
        var phone = E1ApiFactory.NewPhone();

        var response = await RequestOtpAsync(factory, phone, peer: "127.0.0.1", forwardedFor: "203.0.113.7");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await RequestIpAsync(phone)).Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task Forwarded_for_from_a_trusted_proxy_becomes_the_client_ip()
    {
        var factory = Create(TrustProxy());
        var phone = E1ApiFactory.NewPhone();

        var response = await RequestOtpAsync(factory, phone, peer: Proxy, forwardedFor: "198.51.100.1");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await RequestIpAsync(phone)).Should().Be("198.51.100.1");
    }

    [Fact]
    public async Task Forwarded_for_from_an_untrusted_peer_is_ignored()
    {
        var factory = Create(TrustProxy());
        var phone = E1ApiFactory.NewPhone();

        await RequestOtpAsync(factory, phone, peer: "192.0.2.50", forwardedFor: "198.51.100.2");

        (await RequestIpAsync(phone)).Should().Be("192.0.2.50");
    }

    [Fact]
    public async Task Loopback_is_not_trusted_unless_listed()
    {
        var factory = Create(TrustProxy());
        var phone = E1ApiFactory.NewPhone();

        await RequestOtpAsync(factory, phone, peer: "127.0.0.1", forwardedFor: "198.51.100.4");

        (await RequestIpAsync(phone)).Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task Only_the_hop_added_by_the_proxy_is_taken()
    {
        var factory = Create(TrustProxy());
        var phone = E1ApiFactory.NewPhone();

        // A client-supplied X-Forwarded-For is kept on the left by the proxy; only the rightmost entry is believed.
        await RequestOtpAsync(factory, phone, peer: Proxy, forwardedFor: "6.6.6.6, 198.51.100.3");

        (await RequestIpAsync(phone)).Should().Be("198.51.100.3");
    }

    [Fact]
    public async Task Invalid_known_network_stops_the_host()
    {
        // A generic host with the Api's own registration: WebApplicationFactory sometimes reports
        // ObjectDisposedException instead of the startup error when the Api entry point fails.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ForwardedHeaders:KnownNetworks:0"] = "not-a-network" });
        builder.Services.AddFreshOrbitForwardedHeaders();
        using var host = builder.Build();

        var start = () => host.StartAsync(TestContext.Current.CancellationToken);

        (await start.Should().ThrowAsync<OptionsValidationException>()).Which.OptionsType.Should().Be<ForwardedHeadersSettings>();
    }

    [Fact]
    public async Task Otp_request_limit_is_per_real_client_behind_the_proxy()
    {
        var factory = Create(TrustProxy(new() { ["RateLimiting:OtpRequest:PermitLimit"] = "2" }));

        (await RequestOtpAsync(factory, E1ApiFactory.NewPhone(), Proxy, "198.51.100.10")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await RequestOtpAsync(factory, E1ApiFactory.NewPhone(), Proxy, "198.51.100.10")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var third = await RequestOtpAsync(factory, E1ApiFactory.NewPhone(), Proxy, "198.51.100.10");
        var otherClient = await RequestOtpAsync(factory, E1ApiFactory.NewPhone(), Proxy, "198.51.100.11");

        await ShouldBeRateLimitedAsync(third);
        otherClient.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Otp_request_and_otp_verify_have_separate_limits()
    {
        var factory = Create(new()
        {
            ["RateLimiting:OtpRequest:PermitLimit"] = "1",
            ["RateLimiting:OtpVerify:PermitLimit"] = "2",
        });
        var phone = E1ApiFactory.NewPhone();
        var request = await RequestOtpAsync(factory, phone, "192.0.2.1", null);
        var challengeId = (await request.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("challengeId").GetGuid();
        var code = factory.Sms.Sent.Last(m => m.Phone == phone).Text;
        var wrong = code.Contains("000000", StringComparison.Ordinal) ? "111111" : "000000";

        await ShouldBeRateLimitedAsync(await RequestOtpAsync(factory, E1ApiFactory.NewPhone(), "192.0.2.1", null));
        var firstVerify = await VerifyAsync(factory, challengeId, phone, wrong);
        var secondVerify = await VerifyAsync(factory, challengeId, phone, wrong);
        var thirdVerify = await VerifyAsync(factory, challengeId, phone, wrong);

        firstVerify.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        secondVerify.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        await ShouldBeRateLimitedAsync(thirdVerify);
    }

    [Fact]
    public async Task Refresh_is_rate_limited_per_client_ip()
    {
        var factory = Create(new() { ["RateLimiting:Refresh:PermitLimit"] = "2" });

        for (var i = 0; i < 2; i++)
        {
            var refused = await SendAsync(factory, HttpMethod.Post, "/api/v1/auth/refresh", "192.0.2.2", JsonContent.Create(new { refreshToken = "rt_unknown" }));
            refused.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        await ShouldBeRateLimitedAsync(await SendAsync(factory, HttpMethod.Post, "/api/v1/auth/refresh", "192.0.2.2", JsonContent.Create(new { refreshToken = "rt_unknown" })));
        (await SendAsync(factory, HttpMethod.Post, "/api/v1/auth/refresh", "192.0.2.3", JsonContent.Create(new { refreshToken = "rt_unknown" })))
            .StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Search_endpoints_share_the_search_limit()
    {
        var factory = Create(new() { ["RateLimiting:Search:PermitLimit"] = "1" });

        (await SendAsync(factory, HttpMethod.Get, "/api/v1/products", "192.0.2.4")).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

        await ShouldBeRateLimitedAsync(await SendAsync(factory, HttpMethod.Get, "/api/v1/products", "192.0.2.4"));
        await ShouldBeRateLimitedAsync(await SendAsync(factory, HttpMethod.Get, "/api/v1/search/suggest?q=seer", "192.0.2.4"));
    }

    [Fact]
    public async Task Public_endpoints_share_the_public_limit()
    {
        var factory = Create(new() { ["RateLimiting:Public:PermitLimit"] = "1" });

        (await SendAsync(factory, HttpMethod.Get, "/api/v1/categories", "192.0.2.5")).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

        string[] publicPaths = ["/api/v1/home", "/api/v1/products/seer-fish", "/api/v1/serviceability?pincode=600041", "/api/v1/cities", "/api/v1/delivery-slots", "/api/v1/availability"];
        foreach (var path in publicPaths)
        {
            await ShouldBeRateLimitedAsync(await SendAsync(factory, HttpMethod.Get, path, "192.0.2.5"));
        }

        // Search keeps its own bucket.
        (await SendAsync(factory, HttpMethod.Get, "/api/v1/products", "192.0.2.5")).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Coupon_limit_on_the_cart_coupon_route_is_per_customer()
    {
        var factory = Create(new() { ["RateLimiting:Coupon:PermitLimit"] = "1" });
        using var first = factory.CreateCustomerClient();
        using var second = factory.CreateCustomerClient();

        (await PutCouponAsync(first)).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        await ShouldBeRateLimitedAsync(await PutCouponAsync(first));
        (await PutCouponAsync(second)).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Coupon_limit_is_per_ip_for_guests_and_separate_from_the_cart_limit()
    {
        var factory = Create(new() { ["RateLimiting:Coupon:PermitLimit"] = "1" });

        (await SendAsync(factory, HttpMethod.Put, CouponPath, "192.0.2.6", JsonContent.Create(new { code = "GUESS1" })))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await ShouldBeRateLimitedAsync(await SendAsync(factory, HttpMethod.Put, CouponPath, "192.0.2.6", JsonContent.Create(new { code = "GUESS2" })));
        (await SendAsync(factory, HttpMethod.Put, CouponPath, "192.0.2.7", JsonContent.Create(new { code = "GUESS3" })))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The rest of the cart keeps the public bucket.
        (await SendAsync(factory, HttpMethod.Get, "/api/v1/cart", "192.0.2.6")).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Guest_cart_and_banners_share_the_public_limit()
    {
        var factory = Create(new() { ["RateLimiting:Public:PermitLimit"] = "1" });

        (await SendAsync(factory, HttpMethod.Get, "/api/v1/cart", "192.0.2.8")).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

        await ShouldBeRateLimitedAsync(await SendAsync(factory, HttpMethod.Get, "/api/v1/cart", "192.0.2.8"));
        await ShouldBeRateLimitedAsync(await SendAsync(factory, HttpMethod.Get, "/api/v1/promotions", "192.0.2.8"));
    }

    private static Dictionary<string, string?> TrustProxy(Dictionary<string, string?>? more = null)
    {
        var settings = more ?? [];
        settings["ForwardedHeaders:KnownNetworks:0"] = ProxyNetwork;
        return settings;
    }

    private EdgeApiFactory Create(Dictionary<string, string?> settings)
    {
        var factory = new EdgeApiFactory(database, settings);
        _factories.Add(factory);
        return factory;
    }

    private static Task<HttpResponseMessage> PutCouponAsync(HttpClient client)
        => client.PutAsJsonAsync(CouponPath, new { code = "GUESS" }, Ct);

    private static Task<HttpResponseMessage> RequestOtpAsync(EdgeApiFactory factory, string phone, string peer, string? forwardedFor)
        => SendAsync(factory, HttpMethod.Post, "/api/v1/auth/otp/request", peer, JsonContent.Create(new { phone }), forwardedFor);

    private static Task<HttpResponseMessage> VerifyAsync(EdgeApiFactory factory, Guid challengeId, string phone, string code)
        => SendAsync(factory, HttpMethod.Post, "/api/v1/auth/otp/verify", "192.0.2.1", JsonContent.Create(new { challengeId, phone, code }));

    private static async Task<HttpResponseMessage> SendAsync(EdgeApiFactory factory, HttpMethod method, string path, string peer, HttpContent? content = null, string? forwardedFor = null)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add(EdgeApiFactory.PeerHeader, peer);
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return await client.SendAsync(request, Ct);
    }

    private static async Task ShouldBeRateLimitedAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
        body.GetProperty("code").GetString().Should().Be("RATE_LIMITED");
        body.GetProperty("retryAfterSeconds").GetInt32().Should().BePositive();
    }

    private async Task<string?> RequestIpAsync(string phone)
    {
        await using var db = database.CreateDbContext();
        return await db.Set<OtpChallenge>().Where(c => c.Phone == phone).Select(c => c.RequestIp).SingleAsync(Ct);
    }
}

/// <summary>The real Api with extra settings, a capturing SMS sender and a test-set peer address.</summary>
public sealed class EdgeApiFactory(E1Database database, IReadOnlyDictionary<string, string?> settings) : ApiFactory(database)
{
    /// <summary>Request header carrying the TCP peer address the test wants the server to see.</summary>
    public const string PeerHeader = "X-Test-Peer";

    public CapturingSmsSender Sms { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ISmsSender>(Sms));
        services.AddSingleton<IStartupFilter, PeerAddressStartupFilter>();
    }

    private sealed class PeerAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (IPAddress.TryParse(context.Request.Headers[PeerHeader].ToString(), out var peer))
                {
                    context.Connection.RemoteIpAddress = peer;
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
