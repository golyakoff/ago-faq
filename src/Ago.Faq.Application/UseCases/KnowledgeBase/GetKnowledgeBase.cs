namespace Ago.Faq.Application.UseCases.KnowledgeBase;

public sealed record GetKnowledgeBase(Guid SiteId);

/// <param name="Text">Empty when this site has never saved a knowledge base - see
/// <c>Ago.Faq.Domain.KnowledgeBase</c>'s own remarks on why that is a real state, not an error.</param>
public sealed record KnowledgeBaseRead(string Text, DateTimeOffset? UpdatedAt);
