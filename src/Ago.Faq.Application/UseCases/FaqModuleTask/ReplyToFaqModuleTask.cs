namespace Ago.Faq.Application.UseCases.FaqModuleTask;

/// <param name="ExternalTaskId">This module's own <c>FaqModuleTaskId</c> in string form.</param>
/// <param name="ChatTaskId">Accepted and never stored - see <c>Ago.Faq.Domain.FaqModuleTask</c>'s own
/// remarks.</param>
/// <param name="Kind">Echoes the step's own kind - must be <c>form</c>, the only kind this module ever
/// waits on a reply for.</param>
/// <param name="Value">The visitor's raw typed question text.</param>
/// <param name="CredentialSiteId">`22-02`: the site id <c>Ago.Faq.Api.ModuleTasks.ModuleTaskEndpoints</c>'s
/// own credential check proved this call is for - always present since `22-04` removed the one
/// rollout window that used to leave it null (<c>IModuleCallCredentialValidator</c>'s own remarks);
/// the type stays nullable and <see cref="ReplyToFaqModuleTaskHandler"/> still skips the cross-check
/// rather than assume-and-throw if it ever is. Checked against <c>Domain.FaqModuleTask.SiteId</c> - the one piece of
/// `22-02`'s claim this module can actually enforce that `ago-calendar`'s own reply route cannot yet:
/// this module already stores a site id per task (this file's own <c>StartFaqModuleTask.SiteId</c>
/// remarks), so a credential proven for site A is refused outright against a task that belongs to site
/// B, not merely authenticated and trusted.</param>
public sealed record ReplyToFaqModuleTask(string ExternalTaskId, Guid ChatTaskId, string Kind, string Value, Guid? CredentialSiteId = null);

public sealed record FaqModuleTaskReplied(FaqModuleStep Step, bool Complete);
