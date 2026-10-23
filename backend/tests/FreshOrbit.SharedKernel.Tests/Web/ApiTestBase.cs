using System.Net.Http.Json;
using System.Text.Json;

namespace FreshOrbit.SharedKernel.Tests.Web;

/// <summary>Starts the real Api (plus the widget test module) for each test.</summary>
public abstract class ApiTestBase(SharedKernelDatabase database) : IAsyncLifetime
{
    protected TestApiFactory Factory { get; } = new(database);

    protected SharedKernelDatabase Database => database;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).Clone();
}
