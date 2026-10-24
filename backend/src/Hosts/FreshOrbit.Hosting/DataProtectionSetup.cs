using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Hosting;

/// <summary>Where the ASP.NET Core Data Protection key ring is kept (section <c>DataProtection</c>).</summary>
public sealed class DataProtectionKeysOptions
{
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Absolute directory shared by every Api instance and kept across deploys (a named volume in production). Required
    /// outside Development and Testing (rule 7); left empty there, the framework's per-user default is used.
    /// </summary>
    public string? KeysPath { get; set; }
}

public static class DataProtectionSetup
{
    /// <summary>Application discriminator: payloads protected by one instance or release can be read by every other.</summary>
    public const string ApplicationName = "FreshOrbit";

    /// <summary>
    /// Data Protection with a persisted, shared key ring (E2E-3): keys live in <c>DataProtection:KeysPath</c>, so a
    /// restart, a new release or a second instance can still unprotect what another protected. Nothing in Fresh Orbit
    /// protects payloads today (authentication is JWT bearer); this keeps the framework's own users (authentication
    /// handlers, antiforgery) safe when one is added. Keys are not encrypted by the application: they are protected by
    /// the volume's permissions (app user only) and the host's disk encryption (docs/runbooks/deploy.md).
    /// </summary>
    public static IServiceCollection AddFreshOrbitDataProtection(this IServiceCollection services)
    {
        services.AddOptions<DataProtectionKeysOptions>()
            .BindConfiguration(DataProtectionKeysOptions.SectionName)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DataProtectionKeysOptions>, DataProtectionKeysOptionsValidator>());

        services.AddDataProtection().SetApplicationName(ApplicationName);
        // Registered after AddDataProtection, so it runs after the framework's own setup and wins.
        services.AddSingleton<IConfigureOptions<KeyManagementOptions>, ConfigureKeyRepository>();
        return services;
    }

    private sealed class ConfigureKeyRepository(IOptions<DataProtectionKeysOptions> keys, ILoggerFactory loggerFactory) : IConfigureOptions<KeyManagementOptions>
    {
        public void Configure(KeyManagementOptions options)
        {
            var path = keys.Value.KeysPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(path), loggerFactory);
            }
        }
    }
}

internal sealed class DataProtectionKeysOptionsValidator(IHostEnvironment environment) : IValidateOptions<DataProtectionKeysOptions>
{
    public ValidateOptionsResult Validate(string? name, DataProtectionKeysOptions options)
    {
        var path = options.KeysPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return environment.IsDevelopment() || environment.IsEnvironment("Testing")
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail("DataProtection:KeysPath is required outside Development and Testing; set it to a persistent directory shared by every Api instance.");
        }

        return Path.IsPathFullyQualified(path)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("DataProtection:KeysPath must be an absolute directory path.");
    }
}
