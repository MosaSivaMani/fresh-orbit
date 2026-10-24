using FreshOrbit.Hosting;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Web.Modules;
using FreshOrbit.Worker;
using FreshOrbit.Worker.Jobs;
using Hangfire;
using Hangfire.PostgreSql;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFreshOrbitSerilog(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<ICurrentActor>(SystemActor.Instance);
builder.Services.AddFreshOrbitPersistence();
builder.Services.AddFreshOrbitModules(builder.Configuration, Modules.All);

builder.Services.AddHangfire((services, configuration) => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(
        options => options.UseNpgsqlConnection(services.GetRequiredService<IConfiguration>().GetConnectionString(FreshOrbitDbContextOptions.ConnectionStringName)),
        new PostgreSqlStorageOptions { SchemaName = "hangfire", PrepareSchemaIfNecessary = true }));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<RecurringJobRunner>();
builder.Services.AddHostedService<RecurringJobScheduler>();
builder.Services.AddHostedService<OutboxDispatcherService>();

return await builder.Build().RunFreshOrbitAsync();
