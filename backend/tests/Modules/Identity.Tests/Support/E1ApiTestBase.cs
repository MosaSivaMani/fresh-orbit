using System.Net.Http.Json;
using System.Text.Json;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.E1Testing;

/// <summary>Starts the real Api for each test (see <see cref="E1ApiFactory"/>).</summary>
public abstract class E1ApiTestBase(E1Database database) : IAsyncLifetime
{
    protected E1ApiFactory Factory { get; } = new(database);

    protected E1Database Database => database;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).Clone();
    }

    protected static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ReadJsonAsync(response);
        return body.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>Runs <paramref name="work"/> in a fresh DI scope of the Api (system actor unless the scope says otherwise).</summary>
    protected async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        await using var scope = Factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    protected Task<T> QueryAsync<T>(Func<FreshOrbitDbContext, Task<T>> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return InScopeAsync(provider => query(provider.GetRequiredService<FreshOrbitDbContext>()));
    }
}
