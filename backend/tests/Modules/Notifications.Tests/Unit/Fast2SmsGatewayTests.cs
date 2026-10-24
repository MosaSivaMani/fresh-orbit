using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Notifications.Infrastructure.Senders.Dlt;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Notifications.Tests.Unit;

/// <summary>E2E-1: the Fast2SMS adapter against a fake HTTP handler (no network): request shape, auth header, errors.</summary>
public sealed class Fast2SmsGatewayTests
{
    private static readonly DltSms Sms = new("9876543210", "FRSHOR", "1101000000000000001", "1107000000000001000", "OTP_LOGIN", "123456 is your Fresh Orbit login code.", ["123456", "Fresh Orbit"]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Posts_the_dlt_manual_request_with_the_key_in_the_authorization_header()
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.OK, """{"return":true,"request_id":"lwdtp7cjyqxvfe9","message":["SMS sent successfully."]}"""));

        var id = await Gateway(handler).SendAsync(Sms, SmsSenderUnitTests.Configured(), Ct);

        id.Should().Be("lwdtp7cjyqxvfe9");
        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be("POST");
        request.Uri.Should().Be("https://sms.example.in/dev/bulkV2");
        request.Authorization.Should().Be("key");
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        root.GetProperty("route").GetString().Should().Be("dlt_manual");
        root.GetProperty("sender_id").GetString().Should().Be("FRSHOR");
        root.GetProperty("message").GetString().Should().Be(Sms.Text);
        root.GetProperty("template_id").GetString().Should().Be("1107000000000001000");
        root.GetProperty("entity_id").GetString().Should().Be("1101000000000000001");
        root.GetProperty("numbers").GetString().Should().Be("9876543210");
    }

    [Fact]
    public async Task An_acceptance_without_a_request_id_is_still_a_send()
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.OK, """{"return":true}"""));

        (await Gateway(handler).SendAsync(Sms, SmsSenderUnitTests.Configured(), Ct)).Should().BeNull();
    }

    [Fact]
    public async Task An_empty_base_url_uses_the_documented_endpoint()
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.OK, """{"return":true,"request_id":"x"}"""));
        var options = SmsSenderUnitTests.Configured();
        options.BaseUrl = null;

        await Gateway(handler).SendAsync(Sms, options, Ct);

        handler.Requests.Should().ContainSingle().Which.Uri.Should().Be("https://www.fast2sms.com/dev/bulkV2");
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"return":false,"status_code":412,"message":"Invalid Authentication"}""", SmsFailureKind.Unauthorized, "412")]
    [InlineData(HttpStatusCode.Unauthorized, """{"return":false}""", SmsFailureKind.Unauthorized, "401")]
    [InlineData(HttpStatusCode.OK, """{"return":false,"status_code":995,"message":"Template mismatch"}""", SmsFailureKind.Rejected, "995")]
    [InlineData(HttpStatusCode.BadRequest, """{"return":false,"status_code":"996"}""", SmsFailureKind.Rejected, "996")]
    [InlineData(HttpStatusCode.OK, "not json", SmsFailureKind.Rejected, "200")]
    [InlineData(HttpStatusCode.TooManyRequests, "", SmsFailureKind.Unreachable, "429")]
    [InlineData(HttpStatusCode.BadGateway, "<html>", SmsFailureKind.Unreachable, "502")]
    public async Task Provider_answers_map_to_failure_kinds(HttpStatusCode status, string body, SmsFailureKind kind, string providerStatus)
    {
        var handler = new FakeHandler((_, _) => Json(status, body));

        var failure = await Failure(Gateway(handler));

        failure.Kind.Should().Be(kind);
        failure.ProviderStatus.Should().Be(providerStatus);
        failure.Code.Should().Be("SMS_PROVIDER_UNAVAILABLE");
    }

    [Fact]
    public async Task A_network_error_is_unreachable()
    {
        var handler = new FakeHandler((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("Name or service not known")));

        (await Failure(Gateway(handler))).Kind.Should().Be(SmsFailureKind.Unreachable);
    }

    [Fact]
    public async Task A_timeout_is_unreachable()
    {
        var handler = new FakeHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return Json(HttpStatusCode.OK, """{"return":true,"request_id":"late"}""");
        });

        var failure = await Failure(Gateway(handler, TimeSpan.FromMilliseconds(200)));

        failure.Kind.Should().Be(SmsFailureKind.Unreachable);
        failure.ProviderStatus.Should().Be("TIMEOUT");
    }

    [Fact]
    public async Task The_caller_cancelling_is_not_a_provider_failure()
    {
        var handler = new FakeHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return Json(HttpStatusCode.OK, "{}");
        });
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var send = () => Gateway(handler).SendAsync(Sms, SmsSenderUnitTests.Configured(), cancel.Token);

        await send.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async Task<SmsDeliveryException> Failure(Fast2SmsGateway gateway)
    {
        var send = () => gateway.SendAsync(Sms, SmsSenderUnitTests.Configured(), Ct);
        return (await send.Should().ThrowAsync<SmsDeliveryException>()).Which;
    }

    private static Fast2SmsGateway Gateway(FakeHandler handler, TimeSpan? timeout = null)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(Fast2SmsGateway.HttpClientName)
            .ConfigureHttpClient(c => c.Timeout = timeout ?? TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        return new Fast2SmsGateway(services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>());
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

/// <summary>Records each request (method, URI, authorization header, body) and answers with <c>respond</c>.</summary>
public sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public FakeHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
        : this((request, token) => Task.FromResult(respond(request, token)))
    {
    }

    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var authorization = request.Headers.TryGetValues("authorization", out var values) ? string.Join(",", values) : null;
        lock (Requests)
        {
            Requests.Add(new RecordedRequest(request.Method.Method, request.RequestUri!.ToString(), authorization, body));
        }

        return await respond(request, cancellationToken);
    }
}

public sealed record RecordedRequest(string Method, string Uri, string? Authorization, string Body);
