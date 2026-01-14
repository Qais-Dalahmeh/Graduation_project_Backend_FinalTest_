using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Xunit;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory = new();

    private NpgsqlConnection _conn = default!;
    private Respawner _respawner = default!;

    public HttpClient Client { get; private set; } = default!;
    public IServiceProvider Services => _factory.Services;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        Client = _factory.CreateClient();

        _conn = new NpgsqlConnection(_factory.ConnectionString);
        await _conn.OpenAsync();

        _respawner = await Respawner.CreateAsync(_conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },
            TablesToIgnore = new Table[] { "__EFMigrationsHistory" }
        });
    }

    public Task ResetAsync() => _respawner.ResetAsync(_conn);

    public async Task DisposeAsync()
    {
        await _conn.DisposeAsync();
        await _factory.DisposeAsync();
    }
}
