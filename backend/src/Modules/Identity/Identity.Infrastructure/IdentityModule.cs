using FluentValidation;
using FreshOrbit.Identity.Application.Auth;
using FreshOrbit.Identity.Application.Bootstrap;
using FreshOrbit.Identity.Application.Roles;
using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.Identity.Application.Staff;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.Identity.Infrastructure.Endpoints;
using FreshOrbit.Identity.Infrastructure.Persistence;
using FreshOrbit.Identity.Infrastructure.Persistence.Repositories;
using FreshOrbit.Identity.Infrastructure.Public;
using FreshOrbit.Identity.Infrastructure.Security;
using FreshOrbit.Identity.Infrastructure.Seeding;
using FreshOrbit.Identity.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Identity.Infrastructure;

/// <summary>Composition root of the Identity module.</summary>
public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<IdentityModuleModel>();

        services.AddOptions<IdentitySettings>().BindConfiguration(IdentitySettings.SectionName);
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<IdentitySettings>>().Value);

        // AccessTokenFactory / JwtOptions are registered by the Api host; the Worker and Migrations hosts only need
        // what the Public API and the first-admin command use, so the JWT options are added here as well.
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName);
        services.TryAddSingleton<AccessTokenFactory>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOtpChallengeRepository, OtpChallengeRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IStaffLoginFailureRepository, StaffLoginFailureRepository>();
        services.AddScoped<IStaffReads, StaffReads>();

        services.AddSingleton<IOtpCodeHasher, OtpCodeHasher>();
        services.AddSingleton<IPasswordHashing, PasswordHashing>();
        services.AddSingleton<IAccessTokenIssuer, AccessTokenIssuer>();
        services.AddScoped<IRequestContext, HttpRequestContext>();

        services.AddScoped<SessionIssuer>();
        services.AddScoped<SessionRevoker>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IStaffAuthService, StaffAuthService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IFirstAdminService, FirstAdminService>();
        services.AddScoped<IIdentityApi, IdentityApi>();

        services.AddScoped<IValidator<OtpRequestRequest>, OtpRequestRequestValidator>();
        services.AddScoped<IValidator<OtpVerifyRequest>, OtpVerifyRequestValidator>();
        services.AddScoped<IValidator<RefreshRequest>, RefreshRequestValidator>();
        services.AddScoped<IValidator<StaffLoginRequest>, StaffLoginRequestValidator>();
        services.AddScoped<IValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>();
        services.AddScoped<IValidator<CreateStaffRequest>, CreateStaffRequestValidator>();
        services.AddScoped<IValidator<UpdateStaffRequest>, UpdateStaffRequestValidator>();
        services.AddScoped<IValidator<StaffListRequest>, StaffListRequestValidator>();
        services.AddScoped<IValidator<CreateRoleRequest>, CreateRoleRequestValidator>();
        services.AddScoped<IValidator<UpdateRoleRequest>, UpdateRoleRequestValidator>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IReferenceDataLoader, IdentityReferenceDataLoader>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISampleDataLoader, IdentitySampleDataLoader>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => IdentityEndpoints.Map(endpoints);
}
