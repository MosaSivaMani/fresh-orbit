using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshOrbit.Notifications.Application.Shared;

namespace FreshOrbit.Notifications.Infrastructure.Senders.Dlt;

/// <summary>
/// Fast2SMS adapter, "DLT SMS (Manual)" route (E2E-1, <c>docs/research/sms-provider-2026-10.md</c>):
/// <c>POST {BaseUrl}/dev/bulkV2</c> with the API key in the <c>authorization</c> header and a JSON body carrying the
/// route <c>dlt_manual</c>, sender id, full DLT-approved text, DLT template id, principal entity id and the number.
/// The provider forwards these to the operator as they are, so the text must match the registered template exactly.
/// Success is <c>{"return": true, "request_id": "…"}</c>; anything else is an <see cref="SmsDeliveryException"/>.
/// </summary>
public sealed class Fast2SmsGateway(IHttpClientFactory httpClientFactory) : IDltSmsGateway
{
    public const string GatewayName = "FAST2SMS";

    public const string HttpClientName = "FreshOrbit.Sms.Fast2Sms";

    /// <summary>The documented public endpoint, used when <c>Notifications:Sms:Dlt:BaseUrl</c> is empty.</summary>
    public const string DefaultBaseUrl = "https://www.fast2sms.com/";

    public const string Route = "dlt_manual";

    /// <summary>Fast2SMS's <c>status_code</c> for a wrong or missing API key (not verified against a live account).</summary>
    private const string InvalidKeyStatus = "412";

    public string Name => GatewayName;

    public async Task<string?> SendAsync(DltSms sms, DltSmsOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sms);
        ArgumentNullException.ThrowIfNull(options);
        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? DefaultBaseUrl : options.BaseUrl.Trim();
        var endpoint = new Uri(new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/", UriKind.Absolute), "dev/bulkV2");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new Fast2SmsRequest(Route, sms.SenderId, sms.Text, sms.DltTemplateId, sms.PrincipalEntityId, sms.NationalNumber)),
        };
        request.Headers.TryAddWithoutValidation("authorization", options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new SmsDeliveryException(SmsFailureKind.Unreachable, null, exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed.
            throw new SmsDeliveryException(SmsFailureKind.Unreachable, "TIMEOUT", exception);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            var body = await ReadAsync(response, cancellationToken);
            var providerStatus = body?.StatusCode ?? status.ToString(CultureInfo.InvariantCulture);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || body?.StatusCode == InvalidKeyStatus)
            {
                throw new SmsDeliveryException(SmsFailureKind.Unauthorized, providerStatus);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500)
            {
                throw new SmsDeliveryException(SmsFailureKind.Unreachable, providerStatus);
            }

            if (response.IsSuccessStatusCode && body is { Return: true })
            {
                return body.RequestId;
            }

            throw new SmsDeliveryException(SmsFailureKind.Rejected, providerStatus);
        }
    }

    private static async Task<Fast2SmsResponse?> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var returned = root.TryGetProperty("return", out var r) && r.ValueKind == JsonValueKind.True;
            var requestId = root.TryGetProperty("request_id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
            string? statusCode = null;
            if (root.TryGetProperty("status_code", out var code))
            {
                statusCode = code.ValueKind switch
                {
                    JsonValueKind.Number => code.GetRawText(),
                    JsonValueKind.String => code.GetString(),
                    _ => null,
                };
            }

            return new Fast2SmsResponse(returned, requestId, statusCode);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Fast2SmsRequest(
        [property: JsonPropertyName("route")] string Route,
        [property: JsonPropertyName("sender_id")] string SenderId,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("template_id")] string TemplateId,
        [property: JsonPropertyName("entity_id")] string EntityId,
        [property: JsonPropertyName("numbers")] string Numbers);

    private sealed record Fast2SmsResponse(bool Return, string? RequestId, string? StatusCode);
}
