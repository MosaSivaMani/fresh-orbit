using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.SharedKernel.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Notifications.Infrastructure.Senders.Fcm;

/// <summary>The fields of a Google service account key that FCM needs (D324).</summary>
/// <param name="ClientEmail">The service account e-mail, the JWT issuer.</param>
/// <param name="PrivateKeyPem">The PKCS#8 RSA private key in PEM, which signs the JWT.</param>
/// <param name="TokenUri">Google's OAuth 2.0 token endpoint named in the key (https).</param>
public sealed record FcmServiceAccount(string ClientEmail, string PrivateKeyPem, Uri TokenUri)
{
    /// <summary>Google's documented token endpoint, used when the key has no <c>token_uri</c>.</summary>
    public const string DefaultTokenUri = "https://oauth2.googleapis.com/token";

    /// <summary>Parses the key JSON; <paramref name="problem"/> names what is wrong (never the key's content).</summary>
    public static FcmServiceAccount? Parse(string? json, out string? problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            problem = "missing";
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                problem = "not a JSON object";
                return null;
            }

            var email = Text(root, "client_email");
            var key = Text(root, "private_key");
            var tokenUri = Text(root, "token_uri") ?? DefaultTokenUri;
            if (email is null || key is null)
            {
                problem = "client_email and private_key are required";
                return null;
            }

            if (!Uri.TryCreate(tokenUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                problem = "token_uri must be https";
                return null;
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(key);
            return new FcmServiceAccount(email, key, uri);
        }
        catch (JsonException)
        {
            problem = "not valid JSON";
            return null;
        }
        catch (ArgumentException)
        {
            problem = "private_key is not an RSA key in PEM";
            return null;
        }
        catch (CryptographicException)
        {
            problem = "private_key is not an RSA key in PEM";
            return null;
        }
    }

    private static string? Text(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
}

/// <summary>The checks on <c>Notifications:Push:Fcm</c>, shared by the start-up validator and the sender.</summary>
public static class FcmSettings
{
    private const string Section = PushOptions.SectionName + ":Fcm";

    /// <summary>The settings to fix, each named with its full key; empty when the configuration is complete.</summary>
    public static IReadOnlyList<string> Problems(FcmOptions fcm)
    {
        ArgumentNullException.ThrowIfNull(fcm);
        var problems = new List<string>();
        var projectId = fcm.ProjectId?.Trim() ?? string.Empty;
        if (projectId.Length == 0 || !projectId.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
        {
            problems.Add($"{Section}:ProjectId (lower-case letters, digits and dashes)");
        }

        if (FcmServiceAccount.Parse(fcm.ServiceAccountJson, out var problem) is null)
        {
            problems.Add($"{Section}:ServiceAccountJson ({problem})");
        }

        if (fcm.TimeoutSeconds is < 1 or > 60)
        {
            problems.Add($"{Section}:TimeoutSeconds (1 to 60)");
        }

        return problems;
    }
}

/// <summary>
/// Firebase Cloud Messaging HTTP v1 adapter (D324, BE-3 / L-20). Signs a JWT with the service account key, trades it
/// for an OAuth 2.0 access token (scope <c>firebase.messaging</c>, cached until 5 minutes before it expires) and posts
/// one <c>projects/{ProjectId}/messages:send</c> per device token. A token FCM reports as <c>UNREGISTERED</c> or
/// <c>SENDER_ID_MISMATCH</c> is returned as invalid (the dispatcher removes it). When no device accepted the message
/// it throws a <see cref="PushDeliveryException"/>, retryable for an outage or a refused credential. Logs only counts
/// and codes, never a token, the key or the access token.
/// </summary>
public sealed partial class FcmPushSender(IHttpClientFactory httpClientFactory, IOptionsMonitor<PushOptions> options, IClock clock, ILogger<FcmPushSender> logger) : IPushSender, IDisposable
{
    public const string HttpClientName = "FreshOrbit.Push.Fcm";

    public const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    /// <summary>The FCM HTTP v1 endpoint root.</summary>
    public const string BaseUrl = "https://fcm.googleapis.com/";

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    private static readonly TimeSpan TokenRefreshMargin = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _accessToken;

    private DateTimeOffset _accessTokenExpiresAt;

    public async Task<PushResult> SendAsync(PushMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var fcm = options.CurrentValue.Fcm;
        var account = FcmSettings.Problems(fcm).Count == 0 ? FcmServiceAccount.Parse(fcm.ServiceAccountJson, out _) : null;
        if (account is null)
        {
            throw new PushDeliveryException(PushFailureKind.NotConfigured);
        }

        var accessToken = await AccessTokenAsync(account, cancellationToken);
        var endpoint = new Uri(new Uri(BaseUrl), $"v1/projects/{Uri.EscapeDataString(fcm.ProjectId!.Trim())}/messages:send");
        var invalid = new List<string>();
        var failures = new List<PushDeliveryException>();
        string? firstName = null;
        var accepted = 0;
        foreach (var token in message.Tokens)
        {
            var outcome = await SendOneAsync(endpoint, accessToken, token, message, cancellationToken);
            switch (outcome)
            {
                case { Name: not null }:
                    accepted++;
                    firstName ??= string.IsNullOrEmpty(outcome.Name) ? null : outcome.Name;
                    break;
                case { TokenInvalid: true }:
                    invalid.Add(token);
                    break;
                case { Failure: not null }:
                    failures.Add(outcome.Failure);
                    break;
            }
        }

        if (failures.Any(f => f.Kind == PushFailureKind.Unauthorized))
        {
            // The cached access token may have been revoked; the next send fetches a new one.
            ForgetAccessToken();
        }

        var failureCodes = string.Join(",", failures.Select(f => f.FailureCode).Distinct(StringComparer.Ordinal));
        LogSent(message.Tokens.Count, accepted, invalid.Count, failureCodes.Length == 0 ? "-" : failureCodes);
        if (accepted == 0 && failures.Count > 0)
        {
            var failure = failures.FirstOrDefault(f => f.IsRetryable) ?? failures[0];
            throw new PushDeliveryException(failure.Kind, invalid, failure.ProviderStatus, failure.InnerException);
        }

        return new PushResult(firstName, invalid, accepted);
    }

    public void Dispose()
    {
        _tokenLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<SendOutcome> SendOneAsync(Uri endpoint, string accessToken, string token, PushMessage message, CancellationToken cancellationToken)
    {
        var body = new
        {
            message = new
            {
                token,
                notification = new { title = message.Title, body = message.Body },
                data = message.Data,
            },
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage response;
        try
        {
            response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return SendOutcome.Failed(new PushDeliveryException(PushFailureKind.Unreachable, innerException: exception));
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed.
            return SendOutcome.Failed(new PushDeliveryException(PushFailureKind.Unreachable, providerStatus: "TIMEOUT", innerException: exception));
        }

        using (response)
        {
            using var document = await ReadAsync(response, cancellationToken);
            var root = document?.RootElement;
            if (response.IsSuccessStatusCode)
            {
                var name = root is { ValueKind: JsonValueKind.Object } r && r.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                return new SendOutcome(name ?? string.Empty, false, null);
            }

            var (status, errorCode) = ErrorOf(root);
            var providerStatus = errorCode ?? status ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
            if (errorCode is "UNREGISTERED" or "SENDER_ID_MISMATCH" || (errorCode is null && response.StatusCode == HttpStatusCode.NotFound))
            {
                return new SendOutcome(null, true, null);
            }

            var kind = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => PushFailureKind.Unauthorized,
                _ when errorCode is "THIRD_PARTY_AUTH_ERROR" => PushFailureKind.Unauthorized,
                HttpStatusCode.TooManyRequests => PushFailureKind.Unreachable,
                _ when (int)response.StatusCode >= 500 || errorCode is "QUOTA_EXCEEDED" or "UNAVAILABLE" or "INTERNAL" => PushFailureKind.Unreachable,
                _ => PushFailureKind.Rejected,
            };
            return SendOutcome.Failed(new PushDeliveryException(kind, providerStatus: providerStatus));
        }
    }

    private async Task<string> AccessTokenAsync(FcmServiceAccount account, CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            var now = clock.UtcNow;
            if (_accessToken is not null && now < _accessTokenExpiresAt - TokenRefreshMargin)
            {
                return _accessToken;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, account.TokenUri)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = SignedJwt(account, now),
                }),
            };
            HttpResponseMessage response;
            try
            {
                response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                throw new PushDeliveryException(PushFailureKind.Unreachable, providerStatus: "TOKEN", innerException: exception);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new PushDeliveryException(PushFailureKind.Unreachable, providerStatus: "TOKEN_TIMEOUT", innerException: exception);
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                if (response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500)
                {
                    throw new PushDeliveryException(PushFailureKind.Unreachable, providerStatus: "TOKEN_" + status.ToString(CultureInfo.InvariantCulture));
                }

                using var document = await ReadAsync(response, cancellationToken);
                var root = document?.RootElement;
                var token = root is { ValueKind: JsonValueKind.Object } r && r.TryGetProperty("access_token", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                if (!response.IsSuccessStatusCode || string.IsNullOrEmpty(token))
                {
                    // invalid_grant / invalid_client: a wrong, revoked or disabled key.
                    throw new PushDeliveryException(PushFailureKind.Unauthorized, providerStatus: "TOKEN_" + status.ToString(CultureInfo.InvariantCulture));
                }

                var lifetime = root!.Value.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : TokenLifetime;
                _accessToken = token;
                _accessTokenExpiresAt = now + lifetime;
                return token;
            }
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private void ForgetAccessToken() => _accessToken = null;

    /// <summary>RFC 7523 JWT bearer assertion, RS256, valid for one hour.</summary>
    public static string SignedJwt(FcmServiceAccount account, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(account);
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var issuedAt = now.ToUnixTimeSeconds();
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = account.ClientEmail,
            scope = Scope,
            aud = account.TokenUri.ToString(),
            iat = issuedAt,
            exp = issuedAt + (long)TokenLifetime.TotalSeconds,
        }));
        var signingInput = header + "." + claims;
        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKeyPem);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return signingInput + "." + Base64Url(signature);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>FCM error body: <c>{"error":{"status":"NOT_FOUND","details":[{"errorCode":"UNREGISTERED"}]}}</c>.</summary>
    private static (string? Status, string? ErrorCode) ErrorOf(JsonElement? root)
    {
        if (root is not { ValueKind: JsonValueKind.Object } r || !r.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        var status = error.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        string? errorCode = null;
        if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
        {
            foreach (var detail in details.EnumerateArray())
            {
                if (detail.ValueKind == JsonValueKind.Object && detail.TryGetProperty("errorCode", out var code) && code.ValueKind == JsonValueKind.String)
                {
                    errorCode = code.GetString();
                    break;
                }
            }
        }

        return (status, errorCode);
    }

    private static async Task<JsonDocument?> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "FCM push to {DeviceCount} device(s): {Accepted} accepted, {Invalid} unregistered, failures {FailureCodes}")]
    private partial void LogSent(int deviceCount, int accepted, int invalid, string failureCodes);

    /// <param name="Name">The FCM message name when accepted (empty when FCM gave none).</param>
    private sealed record SendOutcome(string? Name, bool TokenInvalid, PushDeliveryException? Failure)
    {
        public static SendOutcome Failed(PushDeliveryException failure) => new(null, false, failure);
    }
}
