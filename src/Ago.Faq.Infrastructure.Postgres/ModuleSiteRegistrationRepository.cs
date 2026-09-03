using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;
using Ago.Faq.Infrastructure.Postgres.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ago.Faq.Infrastructure.Postgres;

/// <summary>The adapter for <see cref="IModuleSiteRegistrationRepository"/> - a plain point lookup and
/// insert, the same shape <c>KnowledgeBaseRepository</c> uses for its own single-key row.</summary>
public sealed class ModuleSiteRegistrationRepository(AgoFaqDbContext db) : IModuleSiteRegistrationRepository
{
    public Task<ModuleSiteRegistration?> GetBySiteIdAsync(SiteId siteId, CancellationToken cancellationToken) =>
        db.ModuleSiteRegistrations.AsNoTracking().FirstOrDefaultAsync(r => r.SiteId == siteId, cancellationToken);

    public async Task AddAsync(ModuleSiteRegistration registration, CancellationToken cancellationToken)
    {
        db.ModuleSiteRegistrations.Add(registration);
        await db.SaveChangesAsync(cancellationToken);
    }
}
