using Ago.Faq.Application.Abstractions;
using Ago.Faq.Contracts;
using Ago.Platform.Kernel;

namespace Ago.Faq.Application.UseCases.FaqModuleTask;

/// <summary>
/// Advances one <see cref="Domain.FaqModuleTask"/> by its one and only reply - the question the
/// <c>form</c> step asked for. Unlike <c>Ago.Calendar.Application.UseCases.ChatModuleTask.ReplyToModuleTaskHandler</c>,
/// which branches on four different waiting states, this handler has exactly one: a reply either
/// answers the pending question or it does not belong to this task at all.
/// </summary>
public sealed class ReplyToFaqModuleTaskHandler(
    IFaqModuleTaskStore tasks, AnswerFaqQuestionHandler answerHandler, IClock clock)
{
    public async Task<Result<FaqModuleTaskReplied>> HandleAsync(
        ReplyToFaqModuleTask command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.ExternalTaskId, out var taskGuid))
        {
            return FaqModuleTaskErrors.TaskNotFound();
        }

        var task = await tasks.GetByIdAsync(new Domain.FaqModuleTaskId(taskGuid), cancellationToken);
        if (task is null)
        {
            return FaqModuleTaskErrors.TaskNotFound();
        }

        // `22-02`: a credential proven for one site cannot act on another site's already-started
        // task - the exact property this module can enforce on its reply route that `ago-calendar`'s
        // own reply route cannot yet (that product's ChatBookingTask carries no site id of its own;
        // see ReplyToFaqModuleTask's own remarks). TaskNotFound, not a distinct "forbidden" error:
        // this module has no more to tell a caller holding the wrong site's credential than it would
        // tell a caller who simply guessed a task id that never existed - the same "do not confirm a
        // resource's existence to a caller not entitled to it" reasoning PublicBookingErrors already
        // applies in ago-calendar.
        if (command.CredentialSiteId is { } credentialSiteId && credentialSiteId != task.SiteId.Value)
        {
            return FaqModuleTaskErrors.TaskNotFound();
        }

        if (task.State == Domain.FaqModuleTaskState.Completed)
        {
            return FaqModuleTaskErrors.AlreadyComplete();
        }

        if (command.Kind != FaqStepKinds.Form)
        {
            return FaqModuleTaskErrors.KindMismatch();
        }

        var now = clock.UtcNow;
        var step = await answerHandler.HandleAsync(task.SiteId, command.Value, cancellationToken);
        task.Complete(now);
        await tasks.SaveAsync(task, cancellationToken);

        return Result<FaqModuleTaskReplied>.Success(new FaqModuleTaskReplied(step, Complete: true));
    }
}
