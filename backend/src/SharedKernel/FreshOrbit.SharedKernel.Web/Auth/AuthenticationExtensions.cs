using FreshOrbit.SharedKernel.Actors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FreshOrbit.SharedKernel.Web.Auth;

public static class AuthenticationExtensions
{
    /// <summary>The <c>actor_type</c> claim value of a staff token (<see cref="ActorType.Staff"/> as a code).</summary>
    public const string StaffActorCode = "STAFF";

    /// <summary>
    /// JWT bearer authentication with a symmetric key from <c>Jwt:SigningKey</c> (validated at startup), issuer and
    /// audience checks, claim names kept as issued (<c>sub</c>, <c>perm</c>, …), plus <see cref="HttpCurrentActor"/>
    /// and <see cref="AccessTokenFactory"/>. Options are read when first used, so tests can override configuration.
    /// </summary>
    public static IServiceCollection AddFreshOrbitAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = AccessTokenFactory.CreateKey(jwt),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = FreshOrbitClaimTypes.Subject,
                };
            });
        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.Replace(ServiceDescriptor.Scoped<ICurrentActor, HttpCurrentActor>());
        services.TryAddSingleton<AccessTokenFactory>();
        return services;
    }

    /// <summary>Requires an authenticated caller holding the <c>perm</c> claim <paramref name="permission"/> (else 401/403).</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return builder.RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(FreshOrbitClaimTypes.Permission, permission));
    }

    /// <summary>
    /// Requires an authenticated staff token, whatever its permissions (else 401/403). For staff reference lookups
    /// (own locations, city and variant names) that every admin screen needs.
    /// </summary>
    public static TBuilder RequireStaffActor<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(FreshOrbitClaimTypes.ActorType, StaffActorCode));
}
