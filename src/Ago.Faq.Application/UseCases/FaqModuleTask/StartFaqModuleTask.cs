namespace Ago.Faq.Application.UseCases.FaqModuleTask;

/// <param name="ChatTaskId">Chat's own id. Accepted and never stored - see
/// <c>Ago.Faq.Domain.FaqModuleTask</c>'s own remarks.</param>
/// <param name="SiteId">This module's own correlation key, persisted so a later reply (which carries
/// none) can still resolve the right knowledge base.</param>
/// <param name="ConversationId">Accepted and never stored, same reason as <paramref name="ChatTaskId"/>.</param>
/// <param name="TriggerText">The full visitor message that matched this module's trigger.</param>
public sealed record StartFaqModuleTask(Guid ChatTaskId, Guid SiteId, Guid ConversationId, string TriggerText);

public sealed record FaqModuleTaskStarted(string ExternalTaskId, FaqModuleStep Step, bool Complete);
