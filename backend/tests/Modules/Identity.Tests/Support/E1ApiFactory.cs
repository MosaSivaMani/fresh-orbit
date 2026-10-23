using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.E1Testing;

/// <summary>
/// The real Api with generous HTTP rate limits (tests share one client IP) and capturing SMS / e-mail / push senders.
/// </summary>
public sealed partial class E1ApiFactory(E1Database database) : ApiFactory(database)
{
    public CapturingSmsSender Sms { get; } = new();

    public CapturingEmailSender Email { get; } = new();

    public CapturingPushSender Push { get; } = new();

    public E1Database Database => database;

    /// <summary>Extra registrations (e.g. NSubstitute fakes of other modules' extension points); set before the first client.</summary>
    public Action<IServiceCollection>? AdditionalServices { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimiting:Otp:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Login:PermitLimit", "100000");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ISmsSender>(Sms));
        services.Replace(ServiceDescriptor.Singleton<IEmailSender>(Email));
        services.Replace(ServiceDescriptor.Singleton<IPushSender>(Push));
        AdditionalServices?.Invoke(services);
    }

    /// <summary>A unique Indian mobile number for a test.</summary>
    public static string NewPhone() => "+919" + Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Requests an OTP and returns the challenge id and the code read from the captured SMS.</summary>
    public async Task<(Guid ChallengeId, string Code)> RequestOtpAsync(HttpClient client, string phone, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        var response = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone }, ct);
        if ((int)response.StatusCode != 202)
        {
            throw new InvalidOperationException($"OTP request failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var text = Sms.Sent.Last(m => m.Phone == phone).Text;
        return (body.GetProperty("challengeId").GetGuid(), SixDigits().Match(text).Value);
    }

    /// <summary>Signs a customer in by OTP and returns the login response.</summary>
    public async Task<CustomerSession> SignInCustomerAsync(string? phone, CancellationToken ct)
    {
        phone ??= NewPhone();
        using var client = CreateClient();
        var (challengeId, code) = await RequestOtpAsync(client, phone, ct);
        var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { challengeId, phone, code }, ct);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OTP verify failed: {body}");
        }

        return new CustomerSession(
            phone,
            body.GetProperty("accessToken").GetString()!,
            body.GetProperty("refreshToken").GetString()!,
            body.GetProperty("user").GetProperty("id").GetGuid());
    }

    public HttpClient CreateClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();
}

public sealed record CustomerSession(string Phone, string AccessToken, string RefreshToken, Guid UserId);

public sealed class CapturingSmsSender : ISmsSender
{
    public ConcurrentQueue<SmsMessage> Sent { get; } = new();

    public Task<string?> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        Sent.Enqueue(message);
        return Task.FromResult<string?>("sms-" + Sent.Count);
    }
}

public sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Enqueue(message);
        return Task.FromResult<string?>("email-" + Sent.Count);
    }
}

public sealed class CapturingPushSender : IPushSender
{
    public ConcurrentQueue<PushMessage> Sent { get; } = new();

    /// <summary>When set, the next sends throw (to test retries).</summary>
    public bool Fail { get; set; }

    public Task<PushResult> SendAsync(PushMessage message, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new HttpRequestException("push provider unavailable");
        }

        Sent.Enqueue(message);
        return Task.FromResult(new PushResult("push-" + Sent.Count, []));
    }
}
