using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;
using Ago.Faq.Infrastructure.Postgres.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ago.Faq.Infrastructure.Postgres;

/// <summary>
/// The adapter for <see cref="IKnowledgeBaseRepository"/>. <see cref="UpsertAsync"/> relies on
/// <c>PutKnowledgeBaseHandler</c> having already done the "does one exist" read itself
/// (<see cref="FindBySiteIdAsync"/>) to decide between <c>Domain.KnowledgeBase.Create</c> (a fresh,
/// untracked instance) and <c>UpdateText</c> (mutating the already-tracked instance that read
/// returned) - a brand-new instance is <see cref="EntityState.Detached"/> and needs
/// <c>Add</c>; a mutated tracked instance is already <see cref="EntityState.Modified"/> and needs only
/// <c>SaveChangesAsync</c>. No second existence query here: the handler's own read already answered
/// that question, and re-asking it would be the same query twice in one request for no new
/// information.
/// </summary>
public sealed class KnowledgeBaseRepository(AgoFaqDbContext db) : IKnowledgeBaseRepository
{
    public Task<KnowledgeBase?> FindBySiteIdAsync(SiteId siteId, CancellationToken cancellationToken) =>
        db.KnowledgeBases.FirstOrDefaultAsync(k => k.SiteId == siteId, cancellationToken);

    public async Task UpsertAsync(KnowledgeBase knowledgeBase, CancellationToken cancellationToken)
    {
        if (db.Entry(knowledgeBase).State == EntityState.Detached)
        {
            db.KnowledgeBases.Add(knowledgeBase);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
