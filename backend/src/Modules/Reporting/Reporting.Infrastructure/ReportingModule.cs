using FreshOrbit.Reporting.Infrastructure.Endpoints;
using FreshOrbit.Reporting.Infrastructure.Jobs;
using FreshOrbit.Reporting.Infrastructure.Persistence;
using FreshOrbit.Reporting.Infrastructure.Reports;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshOrbit.Reporting.Infrastructure;

/// <summary>Composition root of the Reporting module.</summary>
public sealed class ReportingModule : IModule
{
    public string Name => "Reporting";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleModel<ReportingModuleModel>();
        services.TryAddScoped<ReportReader>();
        services.TryAddScoped<RefreshReportsJob>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRecurringJobRegistration, ReportingJobRegistration>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => ReportingEndpoints.Map(endpoints);
}
