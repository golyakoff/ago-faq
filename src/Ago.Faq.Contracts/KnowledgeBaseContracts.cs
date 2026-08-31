namespace Ago.Faq.Contracts;

/// <summary>
/// The console-facing knowledge-base configuration surface - this item's own new endpoint, not part
/// of the module-task wire contract Chat drives. <c>GET</c> and <c>PUT</c> share one response shape
/// deliberately: a tenant that just saved new text sees exactly what a subsequent read would show,
/// with no second round trip needed to confirm the save took effect.
/// </summary>
/// <param name="Text">Empty string, never null, when no knowledge base has been configured yet - a
/// site with nothing saved is a real, legitimate state (`Ago.Faq.Domain.KnowledgeBase`'s own remarks),
/// not a 404.</param>
/// <param name="UpdatedAt">Null exactly when <paramref name="Text"/> has never been saved.</param>
public sealed record KnowledgeBaseResponse(string Text, DateTimeOffset? UpdatedAt);

public sealed record PutKnowledgeBaseRequest(string Text);
