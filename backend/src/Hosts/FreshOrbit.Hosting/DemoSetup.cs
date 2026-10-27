using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace FreshOrbit.Hosting;

/// <summary>Configuration section <c>Hosting</c> of the Api.</summary>
public sealed class ApiHostingOptions
{
    public const string SectionName = "Hosting";

    public const string RunBackgroundJobsInApiKey = SectionName + ":" + nameof(RunBackgroundJobsInApi);

    /// <summary>
    /// Runs the Worker's background jobs (Hangfire server, recurring jobs, outbox dispatcher) inside the Api process.
    /// Allowed only in the <see cref="DemoSetup.EnvironmentName"/> environment, where there is no room for a second
    /// process (the free demo, <c>docs/runbooks/render-demo.md</c>); everywhere else the Api refuses to start with it.
    /// Default false.
    /// </summary>
    public bool RunBackgroundJobsInApi { get; set; }
}

/// <summary>
/// The <c>Demo</c> environment: a public, sample-data-only demo on free hosting (<c>docs/runbooks/render-demo.md</c>).
/// It behaves like Development for SMS (the development outbox, readable at <c>/api/v1/dev/sms</c>), sample data and
/// Data Protection keys, so the Api refuses to start unless its database was created empty by the demo itself
/// (<see cref="DemoDatabase"/>). Production and Staging behave exactly as before.
/// </summary>
public static class DemoSetup
{
    public const string EnvironmentName = "Demo";

    /// <summary>The sample set's directory; the demo image sets it (infra/docker/api-demo.Dockerfile).</summary>
    public const string SampleDataPathKey = "Seeding:SampleDataPath";

    public static bool IsDemo(this IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return environment.IsEnvironment(EnvironmentName);
    }

    /// <summary>
    /// The Api's <c>Hosting</c> options (validated at start-up) and, in Demo only: the demo database guard (first
    /// hosted service) and, with <c>Hosting:RunBackgroundJobsInApi</c>, the Worker's background jobs with the system
    /// actor outside requests. Call after the modules and authentication are registered.
    /// </summary>
    public static IServiceCollection AddFreshOrbitApiHosting(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        services.AddOptions<ApiHostingOptions>()
            .BindConfiguration(ApiHostingOptions.SectionName)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ApiHostingOptions>, ApiHostingOptionsValidator>());
        if (!environment.IsDemo())
        {
            return services;
        }

        services.AddHostedService<DemoDatabaseGuard>();
        if (configuration.GetValue<bool>(ApiHostingOptions.RunBackgroundJobsInApiKey))
        {
            services.Replace(ServiceDescriptor.Scoped<ICurrentActor, RequestOrSystemActor>());
            services.AddFreshOrbitBackgroundJobs(configuration);
        }

        return services;
    }

    /// <summary>
    /// Demo only: an image under <c>/media/*</c> that is not on disk (the free host's disk is emptied by every
    /// restart, while the database keeps the image keys) is answered with the sample set's committed picture for the
    /// key when there is one, else with the sample placeholder drawing in the
    /// requested format, rendered once per size and kept in memory; nothing is written to disk. Call right after
    /// <c>UseFreshOrbitMedia()</c>, so files that exist are still served from disk. Does nothing elsewhere.
    /// </summary>
    public static IApplicationBuilder UseFreshOrbitDemoMedia(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.ApplicationServices.GetRequiredService<IHostEnvironment>().IsDemo())
        {
            return app;
        }

        // The sample set's directory (Seeding:SampleDataPath, set in the demo image) holds the committed pictures.
        var sampleDirectory = app.ApplicationServices.GetRequiredService<IConfiguration>()[SampleDataPathKey];
        var placeholders = new DemoMediaPlaceholders(sampleDirectory);
        return app.Use(async (context, next) =>
        {
            var request = context.Request;
            var read = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);
            if (read
                && request.Path.StartsWithSegments(FileStorageExtensions.MediaRequestPath, StringComparison.OrdinalIgnoreCase, out var remaining)
                && remaining.Value is { Length: > 1 } key
                && DemoMediaPlaceholders.TryGetFormat(key, out var format, out var contentType))
            {
                var bytes = placeholders.For(key, format);
                var response = context.Response;
                response.ContentType = contentType;
                response.ContentLength = bytes.Length;
                response.Headers[HeaderNames.CacheControl] = "public, max-age=3600";
                response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
                if (HttpMethods.IsGet(request.Method))
                {
                    await response.Body.WriteAsync(bytes, context.RequestAborted);
                }

                return;
            }

            await next(context);
        });
    }
}

internal sealed class ApiHostingOptionsValidator(IHostEnvironment environment) : IValidateOptions<ApiHostingOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiHostingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.RunBackgroundJobsInApi && !environment.IsDemo()
            ? ValidateOptionsResult.Fail($"{ApiHostingOptions.RunBackgroundJobsInApiKey} is allowed only in the {DemoSetup.EnvironmentName} environment; set it to false and run FreshOrbit.Worker for background jobs.")
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// The Api's actor when it also runs background jobs (Demo): the request's JWT actor inside a request, and the system
/// actor in a scope created outside one (Hangfire jobs, the outbox dispatcher), as in FreshOrbit.Worker.
/// </summary>
internal sealed class RequestOrSystemActor : ICurrentActor
{
    private readonly ICurrentActor _actor;

    public RequestOrSystemActor(IHttpContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        _actor = accessor.HttpContext is null ? SystemActor.Instance : new HttpCurrentActor(accessor);
    }

    public ActorType ActorType => _actor.ActorType;

    public Guid? UserId => _actor.UserId;

    public Guid? StaffMemberId => _actor.StaffMemberId;

    public Guid? CustomerId => _actor.CustomerId;

    public IReadOnlySet<string> Permissions => _actor.Permissions;

    public IReadOnlySet<Guid> LocationIds => _actor.LocationIds;

    public bool IsAllLocations => _actor.IsAllLocations;
}
