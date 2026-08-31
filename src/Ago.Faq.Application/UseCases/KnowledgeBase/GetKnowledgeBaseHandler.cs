using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;
using Ago.Platform.Kernel;

namespace Ago.Faq.Application.UseCases.KnowledgeBase;

public sealed class GetKnowledgeBaseHandler(IKnowledgeBaseRepository knowledgeBases)
{
    public async Task<Result<KnowledgeBaseRead>> HandleAsync(
        GetKnowledgeBase query, CancellationToken cancellationToken)
    {
        var knowledgeBase = await knowledgeBases.FindBySiteIdAsync(new SiteId(query.SiteId), cancellationToken);

        return Result<KnowledgeBaseRead>.Success(
            knowledgeBase is null
                ? new KnowledgeBaseRead(string.Empty, UpdatedAt: null)
                : new KnowledgeBaseRead(knowledgeBase.Text, knowledgeBase.UpdatedAt));
    }
}
