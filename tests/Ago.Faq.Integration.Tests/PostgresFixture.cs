using Ago.Faq.Infrastructure.Postgres.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Ago.Faq.Integration.Tests;

/// <summary>
/// One real Postgres per collection, this module's migrations applied once from scratch - which is
/// itself the first thing this suite proves (testing.md, ago-root). Tests isolate themselves with
/// fresh site/task ids rather than by truncating, so nothing depends on execution order - the same
/// shape <c>Ago.Calendar.Integration.Tests.PostgresFixture</c> uses.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public AgoFaqDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AgoFaqDbContext>().UseNpgsql(ConnectionString).Options;
        return new AgoFaqDbContext(options);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
