using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;
using Ago.Platform.Kernel;

namespace Ago.Faq.Application.UseCases.KnowledgeBase;

/// <summary>
/// The console's own save. Upserts unconditionally - see <see cref="IKnowledgeBaseRepository"/>'s own
/// remarks on why there is no separate create-vs-update branch here.
/// </summary>
public sealed class PutKnowledgeBaseHandler(IKnowledgeBaseRepository knowledgeBases, IClock clock)
{
    public async Task<Result<KnowledgeBaseRead>> HandleAsync(
        PutKnowledgeBase command, CancellationToken cancellationToken)
    {
        // Qualified as Domain.KnowledgeBase, not a bare `using`-imported KnowledgeBase - this file's
        // own namespace (Ago.Faq.Application.UseCases.KnowledgeBase) shares its last segment with the
        // Domain type's name, the identical CS0118 collision ReplyToFaqModuleTaskHandler's own remarks
        // describe for FaqModuleTask.
        if (command.Text.Length > Domain.KnowledgeBase.MaxTextLength)
        {
            return KnowledgeBaseErrors.TextTooLong();
        }

        var now = clock.UtcNow;
        var siteId = new SiteId(command.SiteId);

        var existing = await knowledgeBases.FindBySiteIdAsync(siteId, cancellationToken);
        var knowledgeBase = existing is null
            ? Domain.KnowledgeBase.Create(siteId, command.Text, now)
            : existing;

        if (existing is not null)
        {
            knowledgeBase.UpdateText(command.Text, now);
        }

        await knowledgeBases.UpsertAsync(knowledgeBase, cancellationToken);

        return Result<KnowledgeBaseRead>.Success(new KnowledgeBaseRead(knowledgeBase.Text, knowledgeBase.UpdatedAt));
    }
}
