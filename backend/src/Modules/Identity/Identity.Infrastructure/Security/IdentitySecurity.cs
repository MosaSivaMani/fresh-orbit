using System.Security.Cryptography;
using System.Text;
using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Web.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Identity.Infrastructure.Security;

/// <summary>HMAC-SHA256 of <c>challengeId:code</c>, keyed by the JWT signing key (a server secret).</summary>
internal sealed class OtpCodeHasher(IOptions<JwtOptions> jwt) : IOtpCodeHasher
{
    public string Hash(Guid challengeId, string code)
    {
        var key = Encoding.UTF8.GetBytes("otp:" + jwt.Value.SigningKey);
        return Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{challengeId:N}:{code}")));
    }
}

/// <summary>ASP.NET Core Identity's PBKDF2 password hasher.</summary>
internal sealed class PasswordHashing : IPasswordHashing
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object User = new();
    private static readonly Lazy<string> DummyHash = new(() => Hasher.HashPassword(User, Guid.NewGuid().ToString()));

    public string Hash(string password) => Hasher.HashPassword(User, password);

    public void VerifyDummy(string password) => Hasher.VerifyHashedPassword(User, DummyHash.Value, password);

    public bool Verify(string hash, string password, out bool needsRehash)
    {
        var result = Hasher.VerifyHashedPassword(User, hash, password);
        needsRehash = result == PasswordVerificationResult.SuccessRehashNeeded;
        return result != PasswordVerificationResult.Failed;
    }
}

/// <summary>Issues JWTs through the shared <see cref="AccessTokenFactory"/>.</summary>
internal sealed class AccessTokenIssuer(AccessTokenFactory factory) : IAccessTokenIssuer
{
    public IssuedAccessToken Issue(AccessTokenClaims claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        var token = factory.Create(new AccessTokenSubject(
            claims.UserId,
            claims.IsStaff ? ActorType.Staff : ActorType.Customer,
            claims.CustomerId,
            claims.StaffMemberId,
            claims.Permissions,
            claims.LocationIds,
            claims.AllLocations));
        return new IssuedAccessToken(token.Token, token.ExpiresAt);
    }
}

internal sealed class HttpRequestContext(IHttpContextAccessor? accessor = null) : IRequestContext
{
    public string? ClientIp => accessor?.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
