namespace Ago.Faq.Application.UseCases.FaqModuleTask;

/// <param name="ExternalTaskId">This module's own <c>FaqModuleTaskId</c> in string form.</param>
/// <param name="ChatTaskId">Accepted and never stored - see <c>Ago.Faq.Domain.FaqModuleTask</c>'s own
/// remarks.</param>
/// <param name="Kind">Echoes the step's own kind - must be <c>form</c>, the only kind this module ever
/// waits on a reply for.</param>
/// <param name="Value">The visitor's raw typed question text.</param>
public sealed record ReplyToFaqModuleTask(string ExternalTaskId, Guid ChatTaskId, string Kind, string Value);

public sealed record FaqModuleTaskReplied(FaqModuleStep Step, bool Complete);
