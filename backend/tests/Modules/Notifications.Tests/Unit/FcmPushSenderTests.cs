using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Notifications.Infrastructure.Senders;
using FreshOrbit.Notifications.Infrastructure.Senders.Fcm;
using FreshOrbit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Notifications.Tests.Unit;

/// <summary>
/// BE-3 / L-20 (D324): the FCM HTTP v1 adapter against a fake HTTP handler (no network): the signed JWT, the token
/// exchange and its cache, the send request, unregistered tokens and the failure codes.
/// </summary>
public sealed class FcmPushSenderTests
{
    public const string ProjectId = "fresh-orbit-test";

    private const string TokenUri = "https://oauth2.example.test/token";

    private const string SendUri = "https://fcm.googleapis.com/v1/projects/fresh-orbit-test/messages:send";

    private static readonly RSA Key = CreateKey();

    private static readonly PushMessage Message = new(["device-a", "device-b"], "Order FO-1", "Your order is being packed.", new Dictionary<string, string> { ["templateCode"] = "ORDER_PACKED" });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A service account key JSON with a key generated for the test run (never a real credential).</summary>
    public static string ServiceAccountJson() => JsonSerializer.Serialize(new Dictionary<string, string>
    {
        ["type"] = "service_account",
        ["project_id"] = ProjectId,
        ["client_email"] = "push@fresh-orbit-test.iam.gserviceaccount.com",
        ["private_key"] = Key.ExportPkcs8PrivateKeyPem(),
        ["token_uri"] = TokenUri,
    });

    [Fact]
    public async Task Sends_one_message_per_device_with_a_bearer_token_from_the_signed_jwt()
    {
        var handler = Fcm((_, _) => Json(HttpStatusCode.OK, """{"name":"projects/fresh-orbit-test/messages/0:1"}"""));

        var result = await Sender(handler).SendAsync(Message, Ct);

        result.AcceptedCount.Should().Be(2);
        result.InvalidTokens.Should().BeEmpty();
        result.ProviderMessageId.Should().Be("projects/fresh-orbit-test/messages/0:1");
        var token = handler.Requests.Should().ContainSingle(r => r.Uri == TokenUri).Subject;
        token.Body.Should().Contain("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer");
        var sends = handler.Requests.Where(r => r.Uri == SendUri).ToList();
        sends.Should().HaveCount(2);
        sends.Should().OnlyContain(r => r.Authorization == "Bearer access-1");
        using var body = JsonDocument.Parse(sends[0].Body);
        var message = body.RootElement.GetProperty("message");
        message.GetProperty("token").GetString().Should().Be("device-a");
        message.GetProperty("notification").GetProperty("title").GetString().Should().Be("Order FO-1");
        message.GetProperty("data").GetProperty("templateCode").GetString().Should().Be("ORDER_PACKED");
    }

    [Fact]
    public void Jwt_is_rs256_signed_by_the_service_account_for_the_messaging_scope()
    {
        var account = FcmServiceAccount.Parse(ServiceAccountJson(), out _)!;
        var now = new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);

        var parts = FcmPushSender.SignedJwt(account, now).Split('.');

        parts.Should().HaveCount(3);
        Key.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), FromBase64Url(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).Should().BeTrue();
        using var claims = JsonDocument.Parse(FromBase64Url(parts[1]));
        claims.RootElement.GetProperty("iss").GetString().Should().Be("push@fresh-orbit-test.iam.gserviceaccount.com");
        claims.RootElement.GetProperty("scope").GetString().Should().Be(FcmPushSender.Scope);
        claims.RootElement.GetProperty("aud").GetString().Should().Be(TokenUri);
        claims.RootElement.GetProperty("iat").GetInt64().Should().Be(now.ToUnixTimeSeconds());
        claims.RootElement.GetProperty("exp").GetInt64().Should().Be(now.ToUnixTimeSeconds() + 3600);
    }

    [Fact]
    public async Task Access_token_is_reused_until_shortly_before_it_expires()
    {
        var handler = Fcm((_, _) => Json(HttpStatusCode.OK, """{"name":"n"}"""));
        var clock = new TestClock();
        var sender = Sender(handler, clock);

        await sender.SendAsync(Message, Ct);
        clock.Advance(TimeSpan.FromMinutes(50));
        await sender.SendAsync(Message, Ct);
        handler.Requests.Count(r => r.Uri == TokenUri).Should().Be(1);

        clock.Advance(TimeSpan.FromMinutes(6));
        await sender.SendAsync(Message, Ct);
        handler.Requests.Count(r => r.Uri == TokenUri).Should().Be(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "UNREGISTERED")]
    [InlineData(HttpStatusCode.Forbidden, "SENDER_ID_MISMATCH")]
    public async Task Unregistered_tokens_are_returned_and_the_others_still_count(HttpStatusCode status, string errorCode)
    {
        var handler = Fcm((body, _) => body.Contains("device-a", StringComparison.Ordinal)
            ? Json(status, ErrorBody(status, errorCode))
            : Json(HttpStatusCode.OK, """{"name":"projects/fresh-orbit-test/messages/2"}"""));

        var result = await Sender(handler).SendAsync(Message, Ct);

        result.InvalidTokens.Should().Equal("device-a");
        result.AcceptedCount.Should().Be(1);
        result.ProviderMessageId.Should().Be("projects/fresh-orbit-test/messages/2");
    }

    [Fact]
    public async Task Every_token_unregistered_is_a_result_with_nothing_accepted()
    {
        var handler = Fcm((_, _) => Json(HttpStatusCode.NotFound, ErrorBody(HttpStatusCode.NotFound, "UNREGISTERED")));

        var result = await Sender(handler).SendAsync(Message, Ct);

        result.AcceptedCount.Should().Be(0);
        result.InvalidTokens.Should().Equal("device-a", "device-b");
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "UNAVAILABLE", PushFailureKind.Unreachable)]
    [InlineData(HttpStatusCode.TooManyRequests, "QUOTA_EXCEEDED", PushFailureKind.Unreachable)]
    [InlineData(HttpStatusCode.InternalServerError, "INTERNAL", PushFailureKind.Unreachable)]
    [InlineData(HttpStatusCode.Unauthorized, "THIRD_PARTY_AUTH_ERROR", PushFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest, "INVALID_ARGUMENT", PushFailureKind.Rejected)]
    public async Task No_device_accepting_throws_the_failure_kind(HttpStatusCode status, string errorCode, PushFailureKind expected)
    {
        var handler = Fcm((_, _) => Json(status, ErrorBody(status, errorCode)));

        var failure = await Failure(Sender(handler));

        failure.Kind.Should().Be(expected);
        failure.ProviderStatus.Should().Be(errorCode);
        failure.FailureCode.Should().StartWith("PUSH_");
    }

    [Fact]
    public async Task An_unreachable_provider_is_retryable()
    {
        var handler = new FakeHandler((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("No such host is known.")));

        var failure = await Failure(Sender(handler));

        failure.Kind.Should().Be(PushFailureKind.Unreachable);
        failure.IsRetryable.Should().BeTrue();
    }

    [Fact]
    public async Task A_refused_service_account_is_unauthorized_and_sends_nothing()
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));

        var failure = await Failure(Sender(handler));

        failure.FailureCode.Should().Be("PUSH_UNAUTHORIZED");
        handler.Requests.Should().NotContain(r => r.Uri == SendUri);
    }

    [Fact]
    public async Task A_failure_on_one_device_with_unregistered_others_keeps_the_invalid_tokens()
    {
        var handler = Fcm((body, _) => body.Contains("device-a", StringComparison.Ordinal)
            ? Json(HttpStatusCode.NotFound, ErrorBody(HttpStatusCode.NotFound, "UNREGISTERED"))
            : Json(HttpStatusCode.ServiceUnavailable, ErrorBody(HttpStatusCode.ServiceUnavailable, "UNAVAILABLE")));

        var failure = await Failure(Sender(handler));

        failure.Kind.Should().Be(PushFailureKind.Unreachable);
        failure.InvalidTokens.Should().Equal("device-a");
    }

    [Fact]
    public async Task An_incomplete_configuration_is_not_configured()
    {
        var handler = Fcm((_, _) => Json(HttpStatusCode.OK, "{}"));

        var failure = await Failure(Sender(handler, configure: o => o.Fcm.ProjectId = null));

        failure.FailureCode.Should().Be("PUSH_NOT_CONFIGURED");
        failure.IsRetryable.Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public void Complete_fcm_settings_have_no_problems()
        => FcmSettings.Problems(new FcmOptions { ProjectId = ProjectId, ServiceAccountJson = ServiceAccountJson() }).Should().BeEmpty();

    [Theory]
    [InlineData(null, "Notifications:Push:Fcm:ServiceAccountJson")]
    [InlineData("not json", "Notifications:Push:Fcm:ServiceAccountJson")]
    [InlineData("""{"client_email":"a@b.c"}""", "Notifications:Push:Fcm:ServiceAccountJson")]
    [InlineData("""{"client_email":"a@b.c","private_key":"-----BEGIN PRIVATE KEY-----\nnope\n-----END PRIVATE KEY-----\n"}""", "Notifications:Push:Fcm:ServiceAccountJson")]
    public void Fcm_settings_name_the_bad_key(string? json, string expected)
    {
        var problems = FcmSettings.Problems(new FcmOptions { ProjectId = ProjectId, ServiceAccountJson = json });

        problems.Should().ContainSingle().Which.Should().StartWith(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Fresh_Orbit")]
    public void Fcm_settings_refuse_a_malformed_project_id(string projectId)
        => FcmSettings.Problems(new FcmOptions { ProjectId = projectId, ServiceAccountJson = ServiceAccountJson() })
            .Should().ContainSingle().Which.Should().StartWith("Notifications:Push:Fcm:ProjectId");

    private static async Task<PushDeliveryException> Failure(FcmPushSender sender)
    {
        var send = () => sender.SendAsync(Message, Ct);
        return (await send.Should().ThrowAsync<PushDeliveryException>()).Which;
    }

    /// <summary>Answers the token endpoint with <c>access-1</c> and every FCM send with <paramref name="send"/> (given the request body).</summary>
    private static FakeHandler Fcm(Func<string, HttpRequestMessage, HttpResponseMessage> send)
        => new(async (request, token) =>
        {
            if (request.RequestUri!.ToString() == TokenUri)
            {
                return Json(HttpStatusCode.OK, """{"access_token":"access-1","expires_in":3600,"token_type":"Bearer"}""");
            }

            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(token);
            return send(body, request);
        });

    private static FcmPushSender Sender(FakeHandler handler, TestClock? clock = null, Action<PushOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(FcmPushSender.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        services.Configure<PushOptions>(o =>
        {
            o.Provider = PushOptions.FcmProvider;
            o.Fcm.ProjectId = ProjectId;
            o.Fcm.ServiceAccountJson = ServiceAccountJson();
            configure?.Invoke(o);
        });
        var provider = services.BuildServiceProvider();
        return new FcmPushSender(provider.GetRequiredService<IHttpClientFactory>(), provider.GetRequiredService<IOptionsMonitor<PushOptions>>(), clock ?? new TestClock(), NullLogger<FcmPushSender>.Instance);
    }

    private static string ErrorBody(HttpStatusCode status, string errorCode)
        => JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["error"] = new Dictionary<string, object>
            {
                ["code"] = (int)status,
                ["message"] = "test",
                ["status"] = status == HttpStatusCode.NotFound ? "NOT_FOUND" : "FAILED",
                ["details"] = new[] { new Dictionary<string, string> { ["@type"] = "type.googleapis.com/google.firebase.fcm.v1.FcmError", ["errorCode"] = errorCode } },
            },
        });

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '='));
    }

    private static RSA CreateKey() => RSA.Create(2048);
}
