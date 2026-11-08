using FluentValidation;
using FreshOrbit.Audit.Application.Entries;
using FreshOrbit.Audit.Infrastructure.DeadLetters;
using FreshOrbit.Audit.Infrastructure.Persistence;
using FreshOrbit.Audit.Infrastructure.Public;
using FreshOrbit.Audit.Infrastructure.Recording;
using FreshOrbit.Audit.Infrastructure.Reads;
using FreshOrbit.Audit.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FreshOrbit.SharedKernel.Web.Modules;

namespace FreshOrbit.Audit.Infrastructure;

/// <summary>Composition root of the Audit module.</summary>
public sealed class AuditModule : IModule
{
    public const string ViewPermission = "audit.view";

    /// <summary>Outbox dead letters: list and replay (BE-2, D296).</summary>
    public const string DeadLettersPermission = "platform.manage";

    public string Name => "Audit";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<AuditModuleModel>();
        services.AddScoped<IAuditApi, AuditApi>();

        // AUD-R6: every admin write is audited in the same SaveChanges (picked up by AddFreshOrbitPersistence).
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IInterceptor, AdminWriteAuditInterceptor>());
        services.AddScoped<IAuditReads, AuditReads>();
        services.AddScoped<IValidator<AuditListRequest>, AuditListRequestValidator>();
        services.AddScoped<IValidator<DeadLetterListRequest>, DeadLetterListRequestValidator>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/admin/audit", async ([AsParameters] AuditListRequest request, IAuditReads reads, CancellationToken ct) => TypedResults.Ok(await reads.ListAsync(request, ct)))
            .WithValidation<AuditListRequest>()
            .RequirePermission(ViewPermission)
            .WithTags("Audit");

        DeadLetterEndpoints.Map(endpoints);
    }
}
