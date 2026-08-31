using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ago.Faq.Infrastructure.Postgres.Persistence;

/// <summary>
/// Design-time only - <c>dotnet ef migrations add</c>/<c>database update</c> need a way to construct
/// <see cref="AgoFaqDbContext"/> without a running host's DI container. The connection string comes
/// from an environment variable and never a literal here - even a throwaway local placeholder is a
/// credential shape this repository does not commit (repositories.md, ago-root: "no secrets, ever").
/// </summary>
public sealed class AgoFaqDbContextFactory : IDesignTimeDbContextFactory<AgoFaqDbContext>
{
    public AgoFaqDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AGO_FAQ_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Set AGO_FAQ_CONNECTION_STRING before running dotnet ef - e.g. " +
                "Host=localhost;Port=5432;Database=ago_faq;Username=...;Password=...");

        var optionsBuilder = new DbContextOptionsBuilder<AgoFaqDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new AgoFaqDbContext(optionsBuilder.Options);
    }
}
