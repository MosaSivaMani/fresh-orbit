using FreshOrbit.Api;
using FreshOrbit.Hosting;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Web;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Modules;
using FreshOrbit.SharedKernel.Web.Storage;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFreshOrbitSerilog(builder.Configuration, builder.Environment);
builder.Services.AddFreshOrbitWeb();
builder.Services.AddFreshOrbitAuthentication();
builder.Services.AddFreshOrbitPersistence();
builder.Services.AddFreshOrbitModules(builder.Configuration, Modules.All);
builder.Services.AddFreshOrbitRateLimiting();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<FreshOrbitDbContext>("database");
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("ETag", "Location", "Idempotent-Replayed", "Retry-After")));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseFreshOrbitWeb();
app.UseFreshOrbitMedia();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs");
}

app.MapHealthChecks("/health");
app.MapGroup("/api/v1").MapFreshOrbitModules();

await app.RunAsync();

/// <summary>Entry point; public for <c>WebApplicationFactory&lt;Program&gt;</c> in tests.</summary>
public partial class Program;
