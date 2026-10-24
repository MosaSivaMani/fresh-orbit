using System.Security.Cryptography;
using AwesomeAssertions;
using FreshOrbit.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FreshOrbit.ArchitectureTests;

/// <summary>E2E-3: the Data Protection key ring is kept in <c>DataProtection:KeysPath</c> and shared by every host.</summary>
public sealed class DataProtectionSetupTests : IDisposable
{
    private const string Purpose = "FreshOrbit.Tests.DataProtection";

    private readonly DirectoryInfo keys = Directory.CreateTempSubdirectory("fo-dp-keys-");

    public void Dispose() => keys.Delete(recursive: true);

    [Fact]
    public async Task Configured_directory_is_the_key_repository()
    {
        using var host = BuildHost(Environments.Production, keys.FullName);
        await host.StartAsync(TestContext.Current.CancellationToken);

        var repository = host.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository;
        Protector(host).Protect("payload");

        repository.Should().BeOfType<FileSystemXmlRepository>()
            .Which.Directory.FullName.Should().Be(keys.FullName);
        keys.GetFiles("key-*.xml").Should().ContainSingle();
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Payload_protected_before_a_restart_is_readable_after_it()
    {
        string protectedPayload;
        using (var first = BuildHost(Environments.Production, keys.FullName))
        {
            await first.StartAsync(TestContext.Current.CancellationToken);
            protectedPayload = Protector(first).Protect("refresh-me");
            await first.StopAsync(TestContext.Current.CancellationToken);
        }

        using var second = BuildHost(Environments.Production, keys.FullName);
        await second.StartAsync(TestContext.Current.CancellationToken);

        Protector(second).Unprotect(protectedPayload).Should().Be("refresh-me");
        await second.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Two_instances_sharing_the_directory_read_each_others_payloads()
    {
        using var a = BuildHost(Environments.Production, keys.FullName);
        using var b = BuildHost(Environments.Production, keys.FullName);
        await a.StartAsync(TestContext.Current.CancellationToken);
        await b.StartAsync(TestContext.Current.CancellationToken);

        var fromA = Protector(a).Protect("from-a");
        var fromB = Protector(b).Protect("from-b");

        Protector(b).Unprotect(fromA).Should().Be("from-a");
        Protector(a).Unprotect(fromB).Should().Be("from-b");
        await a.StopAsync(TestContext.Current.CancellationToken);
        await b.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Separate_key_directories_cannot_read_each_others_payloads()
    {
        var other = Directory.CreateTempSubdirectory("fo-dp-keys-other-");
        try
        {
            using var a = BuildHost(Environments.Production, keys.FullName);
            using var b = BuildHost(Environments.Production, other.FullName);
            await a.StartAsync(TestContext.Current.CancellationToken);
            await b.StartAsync(TestContext.Current.CancellationToken);

            var fromA = Protector(a).Protect("from-a");

            var read = () => Protector(b).Unprotect(fromA);
            read.Should().Throw<CryptographicException>();
            await a.StopAsync(TestContext.Current.CancellationToken);
            await b.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            other.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("Production", null)]
    [InlineData("Staging", "")]
    [InlineData("Production", "relative/keys")]
    public async Task Missing_or_relative_path_is_refused_outside_development(string environment, string? path)
    {
        using var host = BuildHost(environment, path);

        var start = () => host.StartAsync(TestContext.Current.CancellationToken);

        (await start.Should().ThrowAsync<OptionsValidationException>())
            .Which.OptionsType.Should().Be<DataProtectionKeysOptions>();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task Development_and_testing_may_leave_the_path_empty(string environment)
    {
        using var host = BuildHost(environment, path: null);

        await host.StartAsync(TestContext.Current.CancellationToken);

        host.Services.GetRequiredService<IOptions<DataProtectionKeysOptions>>().Value.KeysPath.Should().BeNull();
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static IHost BuildHost(string environment, string? path)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment, DisableDefaults = true });
        if (path is not null)
        {
            builder.Configuration[$"{DataProtectionKeysOptions.SectionName}:{nameof(DataProtectionKeysOptions.KeysPath)}"] = path;
        }

        builder.Services.AddLogging();
        builder.Services.AddFreshOrbitDataProtection();
        return builder.Build();
    }

    private static IDataProtector Protector(IHost host)
        => host.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);
}
