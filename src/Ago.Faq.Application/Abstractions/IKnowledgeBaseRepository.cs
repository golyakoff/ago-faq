using Ago.Faq.Domain;

namespace Ago.Faq.Application.Abstractions;

/// <summary>
/// The port for <see cref="KnowledgeBase"/>. One method for the read every question-answering path
/// needs, and one upsert for the console's own PUT - deliberately not a load/save pair like
/// <see cref="IFaqModuleTaskStore"/>: a knowledge base's whole lifecycle is "does this site have one
/// yet", and forcing a caller to branch on create-vs-update before calling this port would just move
/// that branch out of the one place (the Postgres adapter's own upsert) that can express it as a
/// single statement.
/// </summary>
public interface IKnowledgeBaseRepository
{
    /// <summary>Null when this site has never saved a knowledge base - a real, legitimate state
    /// (<see cref="KnowledgeBase"/>'s own remarks), not an error.</summary>
    Task<KnowledgeBase?> FindBySiteIdAsync(SiteId siteId, CancellationToken cancellationToken);

    /// <summary>Creates the row if this site has none yet, otherwise updates the existing one.</summary>
    Task UpsertAsync(KnowledgeBase knowledgeBase, CancellationToken cancellationToken);
}
