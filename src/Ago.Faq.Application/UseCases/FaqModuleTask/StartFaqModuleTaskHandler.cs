using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;
using Ago.Platform.Kernel;

namespace Ago.Faq.Application.UseCases.FaqModuleTask;

/// <summary>
/// The chat entry point's first step. Two shapes, both handled here rather than split across two
/// handlers, because they share everything except "was a question already supplied":
///
/// <list type="bullet">
/// <item>A bare trigger (<c>/faq</c>, nothing after it) starts a task in
/// <see cref="Domain.FaqModuleTaskState.AwaitingQuestion"/> and sends the <c>form</c> step asking for
/// one - the task stays open, waiting for <see cref="ReplyToFaqModuleTaskHandler"/>.</item>
/// <item>An inline question (<c>/faq what is your return policy</c>) is answered immediately, in this
/// same request: the task is started and completed in one call, and the response already carries the
/// answer (or the escalation).</item>
/// </list>
///
/// <para>Persists the task either way (<see cref="Domain.FaqModuleTask"/>'s own remarks explain why a
/// row must exist even for the immediately-completed case: not because anything reads it again, but
/// because the alternative - branching the store call on whether persistence is "needed" - would be a
/// second code path for a case that costs nothing to keep uniform).</para>
/// </summary>
public sealed class StartFaqModuleTaskHandler(
    IFaqModuleTaskStore tasks,
    AnswerFaqQuestionHandler answerHandler,
    IIdGenerator idGenerator,
    IClock clock)
{
    public async Task<Result<FaqModuleTaskStarted>> HandleAsync(
        StartFaqModuleTask command, CancellationToken cancellationToken)
    {
        var siteId = new SiteId(command.SiteId);
        var now = clock.UtcNow;
        var taskId = new Domain.FaqModuleTaskId(idGenerator.NewId(now));

        var question = TriggerTextParser.ExtractQuestion(command.TriggerText);

        // Qualified, not a bare `Domain.FaqModuleTask.Start(...)` shortened to `FaqModuleTask.Start(...)`:
        // this file's own namespace (Ago.Faq.Application.UseCases.FaqModuleTask) shares its last
        // segment with the Domain type's own name, and the compiler resolves a bare `FaqModuleTask`
        // identifier to the namespace (CS0118) - the same family of collision
        // Ago.Calendar.Application.UseCases.ChatModuleTask.ReplyToModuleTaskHandler's own remarks
        // document for `BookEvent`.
        var task = Domain.FaqModuleTask.Start(taskId, siteId, now);

        if (string.IsNullOrEmpty(question))
        {
            await tasks.AddAsync(task, cancellationToken);

            var formStep = new FaqModuleStep.Form(
                "What would you like to know?", FieldId: "question", FieldLabel: "Your question");
            return Result<FaqModuleTaskStarted>.Success(
                new FaqModuleTaskStarted(task.Id.Value.ToString(), formStep, Complete: false));
        }

        var step = await answerHandler.HandleAsync(siteId, question, cancellationToken);
        task.Complete(now);
        await tasks.AddAsync(task, cancellationToken);

        return Result<FaqModuleTaskStarted>.Success(
            new FaqModuleTaskStarted(task.Id.Value.ToString(), step, Complete: true));
    }
}
