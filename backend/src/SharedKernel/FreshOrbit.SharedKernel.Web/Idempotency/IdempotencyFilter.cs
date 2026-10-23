using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Platform;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Serialization;
using FreshOrbit.SharedKernel.Time;
using FreshOrbit.SharedKernel.Web.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace FreshOrbit.SharedKernel.Web.Idempotency;

/// <summary>
/// Makes a write safe to retry with an <c>Idempotency-Key</c> header, backed by <c>platform.idempotency_keys</c>.
/// <list type="bullet">
/// <item>Scope = HTTP method + route pattern + caller (user id or <c>anonymous</c>); request hash = SHA-256 of
/// method, path, query and body.</item>
/// <item>First request: the key is claimed (row without status), the endpoint runs, and its status, content type,
/// body, <c>Location</c> and <c>ETag</c> are stored for <see cref="Retention"/>. A 5xx or an exception releases the
/// claim so the client can retry. (Refusals raised as exceptions are not stored: nothing was committed.)</item>
/// <item>Same key + same hash: the stored response is replayed with <c>Idempotent-Replayed: true</c>.</item>
/// <item>Same key + different hash: 422 <c>IDEMPOTENCY_KEY_REUSED</c>. Same key while the first request is still
/// running: 409 <c>IDEMPOTENCY_KEY_IN_PROGRESS</c>.</item>
/// </list>
/// Without the header the endpoint runs normally unless the key is required (then 400).
/// </summary>
public sealed class IdempotencyFilter(bool required) : IEndpointFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotent-Replayed";
    public const int MaxKeyLength = 100;

    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var http = context.HttpContext;
        var key = http.Request.Headers[HeaderName].ToString();
        if (key.Length == 0)
        {
            return required
                ? throw new DomainException(ProblemCodes.IdempotencyKeyInvalid, "Send an Idempotency-Key header to make this request safe to retry.", HeaderName)
                : await next(context);
        }

        if (key.Length > MaxKeyLength)
        {
            throw new DomainException(ProblemCodes.IdempotencyKeyInvalid, $"The Idempotency-Key header must be at most {MaxKeyLength} characters.", HeaderName);
        }

        var services = http.RequestServices;
        var db = services.GetRequiredService<FreshOrbitDbContext>();
        var actor = services.GetRequiredService<ICurrentActor>();
        var now = services.GetRequiredService<IClock>().UtcNow;
        var scope = BuildScope(http, actor);
        var requestHash = await HashRequestAsync(http.Request);
        var cancellationToken = http.RequestAborted;

        await db.Database.ExecuteSqlAsync(
            $"DELETE FROM platform.idempotency_keys WHERE scope = {scope} AND key = {key} AND expires_at < {now}",
            cancellationToken);
        var claimed = await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO platform.idempotency_keys (scope, key, request_hash, status_code, response, expires_at, created_at, created_by)
            VALUES ({scope}, {key}, {requestHash}, NULL, NULL, {now.Add(Retention)}, {now}, {actor.UserId})
            ON CONFLICT (scope, key) DO NOTHING
            """,
            cancellationToken);

        if (claimed == 0)
        {
            var existing = await db.Set<IdempotencyRecord>().AsNoTracking()
                .FirstAsync(r => r.Scope == scope && r.Key == key, cancellationToken);
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new DomainException(ProblemCodes.IdempotencyKeyReused, "This Idempotency-Key was already used for a different request; send a new key.", HeaderName);
            }

            if (existing.StatusCode is not { } storedStatus)
            {
                throw new ConflictException(ProblemCodes.IdempotencyKeyInProgress, "A request with this Idempotency-Key is still running; retry later.", HeaderName);
            }

            await ReplayAsync(http, storedStatus, existing.Response);
            return Results.Empty;
        }

        try
        {
            var result = await next(context);
            var (status, stored, body) = await CaptureAsync(http, result);
            if (status >= StatusCodes.Status500InternalServerError)
            {
                await ReleaseAsync(db, scope, key);
            }
            else
            {
                var json = JsonSerializer.Serialize(stored, FreshOrbitJson.Options);
                var completedAt = services.GetRequiredService<IClock>().UtcNow;
                await db.Database.ExecuteSqlAsync(
                    $"""
                    UPDATE platform.idempotency_keys
                    SET status_code = {status}, response = CAST({json} AS jsonb), modified_at = {completedAt}, modified_by = {actor.UserId}
                    WHERE scope = {scope} AND key = {key}
                    """,
                    CancellationToken.None);
            }

            await http.Response.Body.WriteAsync(body, cancellationToken);
            return Results.Empty;
        }
        catch
        {
            await ReleaseAsync(db, scope, key);
            throw;
        }
    }

    private static string BuildScope(HttpContext http, ICurrentActor actor)
    {
        var route = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? http.Request.Path.Value ?? string.Empty;
        var caller = actor.UserId?.ToString() ?? "anonymous";
        var scope = $"{http.Request.Method} {route}|{caller}";
        return scope.Length <= 300 ? scope : scope[..300];
    }

    private static async Task<string> HashRequestAsync(HttpRequest request)
    {
        if (!request.Body.CanSeek)
        {
            throw new InvalidOperationException("Request buffering is not enabled; call UseFreshOrbitWeb() before mapping endpoints.");
        }

        request.Body.Position = 0;
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(Encoding.UTF8.GetBytes($"{request.Method}\n{request.Path}{request.QueryString}\n"));
        var buffer = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(buffer, request.HttpContext.RequestAborted)) > 0)
        {
            sha.AppendData(buffer, 0, read);
        }

        request.Body.Position = 0;
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    private static async Task<(int Status, StoredResponse Stored, ReadOnlyMemory<byte> Body)> CaptureAsync(HttpContext http, object? result)
    {
        var original = http.Response.Body;
        using var buffer = new MemoryStream();
        http.Response.Body = buffer;
        try
        {
            await ToResult(result).ExecuteAsync(http);
        }
        finally
        {
            http.Response.Body = original;
        }

        var body = buffer.ToArray();
        var headers = http.Response.Headers;
        var stored = new StoredResponse(
            http.Response.ContentType,
            Encoding.UTF8.GetString(body),
            headers.Location.Count > 0 ? headers.Location.ToString() : null,
            headers.ETag.Count > 0 ? headers.ETag.ToString() : null);
        return (http.Response.StatusCode, stored, body);
    }

    private static IResult ToResult(object? result) => result switch
    {
        IResult r => r,
        null => Results.Empty,
        string text => Results.Text(text),
        _ => Results.Json(result),
    };

    private static async Task ReplayAsync(HttpContext http, int status, string? responseJson)
    {
        var stored = responseJson is null ? null : JsonSerializer.Deserialize<StoredResponse>(responseJson, FreshOrbitJson.Options);
        http.Response.StatusCode = status;
        http.Response.Headers[ReplayedHeaderName] = "true";
        if (stored is null)
        {
            return;
        }

        if (stored.Location is not null)
        {
            http.Response.Headers[HeaderNames.Location] = stored.Location;
        }

        if (stored.ETag is not null)
        {
            http.Response.Headers[HeaderNames.ETag] = stored.ETag;
        }

        if (stored.ContentType is not null)
        {
            http.Response.ContentType = stored.ContentType;
        }

        await http.Response.WriteAsync(stored.Body, http.RequestAborted);
    }

    private static Task<int> ReleaseAsync(FreshOrbitDbContext db, string scope, string key)
        => db.Database.ExecuteSqlAsync(
            $"DELETE FROM platform.idempotency_keys WHERE scope = {scope} AND key = {key} AND status_code IS NULL",
            CancellationToken.None);

    private sealed record StoredResponse(string? ContentType, string Body, string? Location, string? ETag);
}

public static class IdempotencyEndpointExtensions
{
    /// <summary>Makes the endpoint idempotent per <c>Idempotency-Key</c> (see <see cref="IdempotencyFilter"/>).</summary>
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder, bool required = false)
        => builder.AddEndpointFilter(new IdempotencyFilter(required));

    /// <summary>Buffers request bodies that carry an Idempotency-Key so the filter can hash them after binding.</summary>
    public static IApplicationBuilder UseIdempotencyBuffering(this IApplicationBuilder app)
        => app.Use(static (context, next) =>
        {
            if (context.Request.Headers.ContainsKey(IdempotencyFilter.HeaderName))
            {
                context.Request.EnableBuffering();
            }

            return next(context);
        });
}
