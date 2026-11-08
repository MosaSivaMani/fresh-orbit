using FreshOrbit.Hosting;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Web.Modules;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFreshOrbitSerilog(builder.Configuration, builder.Environment);
// OpenTelemetry traces and metrics; off unless Telemetry:OtlpEndpoint is set (BE-8, D332).
builder.Services.AddFreshOrbitTelemetry(builder.Configuration, "fresh-orbit-worker", web: false);
builder.Services.AddSingleton<ICurrentActor>(SystemActor.Instance);
builder.Services.AddFreshOrbitPersistence();
builder.Services.AddFreshOrbitModules(builder.Configuration, Modules.All);

// Hangfire server, recurring jobs and the outbox dispatcher (shared with the Demo Api, FreshOrbit.Hosting).
builder.Services.AddFreshOrbitBackgroundJobs(builder.Configuration);

return await builder.Build().RunFreshOrbitAsync();
