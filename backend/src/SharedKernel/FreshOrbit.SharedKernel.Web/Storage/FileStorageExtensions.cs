using FreshOrbit.SharedKernel.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace FreshOrbit.SharedKernel.Web.Storage;

public static class FileStorageExtensions
{
    /// <summary>The URL prefix public files are served under.</summary>
    public const string MediaRequestPath = "/media";

    /// <summary>File extensions served publicly under <see cref="MediaRequestPath"/>; anything else (originals) is not served.</summary>
    private static readonly Dictionary<string, string> PublicContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".webp"] = "image/webp",
        [".jpg"] = "image/jpeg",
        [".png"] = "image/png",
    };

    /// <summary>
    /// Registers <see cref="LocalDiskFileStorage"/> as the singleton <see cref="IFileStorage"/> (configuration section
    /// <c>FileStorage</c>). Safe to call from several modules.
    /// </summary>
    public static IServiceCollection AddFreshOrbitFileStorage(this IServiceCollection services)
    {
        services.AddOptions<FileStorageOptions>().BindConfiguration(FileStorageOptions.SectionName);
        services.TryAddSingleton<IFileStorage, LocalDiskFileStorage>();
        return services;
    }

    /// <summary>
    /// Serves stored files under <c>/media/*</c> (images only, cacheable for a year: keys never change content).
    /// Call in the Api host after <c>UseFreshOrbitWeb()</c>.
    /// </summary>
    public static IApplicationBuilder UseFreshOrbitMedia(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var services = app.ApplicationServices;
        var options = services.GetRequiredService<IOptions<FileStorageOptions>>().Value;
        var root = LocalDiskFileStorage.ResolveRootPath(options, services.GetRequiredService<IHostEnvironment>());
        Directory.CreateDirectory(root);
        return app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = MediaRequestPath,
            FileProvider = new PhysicalFileProvider(root),
            ContentTypeProvider = new FileExtensionContentTypeProvider(PublicContentTypes),
            ServeUnknownFileTypes = false,
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
                context.Context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
            },
        });
    }

    /// <summary>For tests and tools: a storage rooted at <paramref name="rootPath"/>.</summary>
    public static LocalDiskFileStorage CreateLocalDisk(string rootPath, string publicBaseUrl = MediaRequestPath, string environmentName = "Development")
        => new(
            Options.Create(new FileStorageOptions { RootPath = rootPath, PublicBaseUrl = publicBaseUrl }),
            new FixedHostEnvironment(environmentName, Path.GetFullPath(rootPath)));

    private sealed class FixedHostEnvironment(string environmentName, string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "FreshOrbit";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

