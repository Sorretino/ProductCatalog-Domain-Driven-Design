using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProductCatalog.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace ProductCatalog.TestSupport;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute([CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = 0) : base(sourceFilePath, sourceLineNumber)
    {
        if (!PostgresFixture.Enabled)
        {
            Skip = "Defina RUN_POSTGRES_TESTS=1 para Docker ou TEST_POSTGRES_CONNECTION para um servidor PostgreSQL de testes.";
        }
    }
}

/// <summary>Creates and drops only its own randomly named database, never the supplied database.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _adminConnection;
    private readonly string _database = "catalog_tests_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; private set; } = null!;
    public static bool Enabled => Environment.GetEnvironmentVariable("RUN_POSTGRES_TESTS") == "1"
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION"));

    public async ValueTask InitializeAsync()
    {
        if (!Enabled) return;
        _adminConnection = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(_adminConnection))
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await _container.StartAsync();
            _adminConnection = _container.GetConnectionString();
        }

        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", connection);
        await create.ExecuteNonQueryAsync();
        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnection) { Database = _database }.ConnectionString;
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public CatalogDbContext CreateContext() => new(new DbContextOptionsBuilder<CatalogDbContext>()
        .UseNpgsql(ConnectionString).Options);

    public async ValueTask DisposeAsync()
    {
        if (_adminConnection is not null)
        {
            // The identifier is generated exclusively from a constant prefix plus a GUID.
            await using var connection = new NpgsqlConnection(_adminConnection);
            await connection.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }

        if (_container is not null) await _container.DisposeAsync();
    }
}
