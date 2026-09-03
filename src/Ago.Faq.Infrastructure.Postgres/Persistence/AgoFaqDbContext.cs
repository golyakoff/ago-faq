using Ago.Faq.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ago.Faq.Infrastructure.Postgres.Persistence;

/// <summary>
/// This module's own database - never a schema inside `Ago.Chat.*`'s or `Ago.Calendar.*`'s (the same
/// "own repository, own deploy" reasoning `19-03`'s own "Decided" section gives for why `ago-faq` is a
/// repository at all). No outbox/inbox tables here - see this project's own .csproj for why.
/// </summary>
public sealed class AgoFaqDbContext(DbContextOptions<AgoFaqDbContext> options) : DbContext(options)
{
    public DbSet<FaqModuleTask> FaqModuleTasks => Set<FaqModuleTask>();

    public DbSet<KnowledgeBase> KnowledgeBases => Set<KnowledgeBase>();

    /// <summary>`22-04`: adr/0065's registry, this module's own consuming half - see
    /// <see cref="ModuleSiteRegistration"/>'s own remarks.</summary>
    public DbSet<ModuleSiteRegistration> ModuleSiteRegistrations => Set<ModuleSiteRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgoFaqDbContext).Assembly);
    }
}
