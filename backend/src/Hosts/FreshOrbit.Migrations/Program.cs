using FreshOrbit.Hosting;
using FreshOrbit.Migrations;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Web.Modules;

var command = MigrationCommand.Parse(args);
if (command is null)
{
    await Console.Error.WriteLineAsync(MigrationCommand.Usage);
    return 1;
}

var builder = Host.CreateApplicationBuilder(MigrationCommand.ConfigurationArgs(args));
builder.Services.AddFreshOrbitSerilog(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<ICurrentActor>(SystemActor.Instance);
builder.Services.AddFreshOrbitPersistence();
builder.Services.AddFreshOrbitModules(builder.Configuration, Modules.All);
builder.Services.AddSingleton<MigrationRunner>();

using var host = builder.Build();
return await host.Services.GetRequiredService<MigrationRunner>().RunAsync(command, CancellationToken.None);
