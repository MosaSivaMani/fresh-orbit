using System.Security.Claims;
using System.Text;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Time;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FreshOrbit.SharedKernel.Web.Auth;

/// <summary>Who an access token is for.</summary>
public sealed record AccessTokenSubject(
    Guid UserId,
    ActorType ActorType,
    Guid? CustomerId = null,
    Guid? StaffMemberId = null,
    IReadOnlyCollection<string>? Permissions = null,
    IReadOnlyCollection<Guid>? LocationIds = null,
    bool AllLocations = false);

/// <summary>An issued access token and its expiry.</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Issues signed JWT access tokens with the FreshOrbit claims (used by Identity and by tests).</summary>
public sealed class AccessTokenFactory(IOptions<JwtOptions> options, IClock clock)
{
    public AccessToken Create(AccessTokenSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var jwt = options.Value;
        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(jwt.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(FreshOrbitClaimTypes.Subject, subject.UserId.ToString()),
            new(FreshOrbitClaimTypes.ActorType, subject.ActorType.ToCode()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (subject.CustomerId is { } customerId)
        {
            claims.Add(new Claim(FreshOrbitClaimTypes.CustomerId, customerId.ToString()));
        }

        if (subject.StaffMemberId is { } staffId)
        {
            claims.Add(new Claim(FreshOrbitClaimTypes.StaffMemberId, staffId.ToString()));
        }

        claims.AddRange((subject.Permissions ?? []).Select(p => new Claim(FreshOrbitClaimTypes.Permission, p)));
        claims.AddRange((subject.LocationIds ?? []).Select(l => new Claim(FreshOrbitClaimTypes.Location, l.ToString())));
        if (subject.AllLocations)
        {
            claims.Add(new Claim(FreshOrbitClaimTypes.AllLocations, "true"));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(CreateKey(jwt), SecurityAlgorithms.HmacSha256),
        };
        return new AccessToken(new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor), expiresAt);
    }

    internal static SymmetricSecurityKey CreateKey(JwtOptions jwt) => new(Encoding.UTF8.GetBytes(jwt.SigningKey));
}
