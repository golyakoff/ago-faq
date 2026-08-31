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
