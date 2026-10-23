using FreshOrbit.SharedKernel.Tests.TestModule;
using FreshOrbit.SharedKernel.Web.Modules;
using FreshOrbit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.SharedKernel.Tests;

/// <summary>The real Api plus the widget test module.</summary>
public sealed class TestApiFactory(SharedKernelDatabase database) : ApiFactory(database)
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        var module = new WidgetTestModule();
        services.AddSingleton<IModule>(module);
        module.Register(services, new ConfigurationBuilder().Build());
    }
}
