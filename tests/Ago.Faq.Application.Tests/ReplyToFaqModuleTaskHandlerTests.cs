using Ago.Faq.Application.Abstractions;
using Ago.Faq.Application.UseCases.FaqModuleTask;
using Ago.Faq.Contracts;
using Ago.Faq.Domain;

namespace Ago.Faq.Application.Tests;

public sealed class ReplyToFaqModuleTaskHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    private static ReplyToFaqModuleTaskHandler BuildHandler(
        InMemoryFaqModuleTaskStore tasks, InMemoryKnowledgeBaseRepository knowledgeBases, FakeFaqAnswerGenerator generator) =>
        new(tasks, new AnswerFaqQuestionHandler(knowledgeBases, generator), new FakeClock(Now));

    [Fact]
    public async Task UnknownTaskId_ReturnsTaskNotFound()
    {
        var handler = BuildHandler(
            new InMemoryFaqModuleTaskStore(), new InMemoryKnowledgeBaseRepository(),
            new FakeFaqAnswerGenerator(new FaqAnswerResult.Answered("unused")));

        var result = await handler.HandleAsync(
            new ReplyToFaqModuleTask(Guid.NewGuid().ToString(), Guid.NewGuid(), FaqStepKinds.Form, "a question"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("faq_module_task.not_found", result.Error!.Value.Code);
    }

    [Fact]
    public async Task MalformedExternalTaskId_ReturnsTaskNotFound()
    {
        var handler = BuildHandler(
            new InMemoryFaqModuleTaskStore(), new InMemoryKnowledgeBaseRepository(),
            new FakeFaqAnswerGenerator(new FaqAnswerResult.Answered("unused")));

        var result = await handler.HandleAsync(
            new ReplyToFaqModuleTask("not-a-guid", Guid.NewGuid(), FaqStepKinds.Form, "a question"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("faq_module_task.not_found", result.Error!.Value.Code);
    }

    [Fact]
    public async Task WrongKind_ReturnsKindMismatch_AndLeavesTaskOpen()
    {
        var tasks = new InMemoryFaqModuleTaskStore();
        var siteId = new SiteId(Guid.NewGuid());
        var taskId = new FaqModuleTaskId(Guid.NewGuid());
        await tasks.AddAsync(Domain.FaqModuleTask.Start(taskId, siteId, Now), CancellationToken.None);

        var handler = BuildHandler(
            tasks, new InMemoryKnowledgeBaseRepository(), new FakeFaqAnswerGenerator(new FaqAnswerResult.Answered("unused")));

        var result = await handler.HandleAsync(
            new ReplyToFaqModuleTask(taskId.Value.ToString(), Guid.NewGuid(), "choice_list", "some-value"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("faq_module_task.kind_mismatch", result.Error!.Value.Code);

        var persisted = await tasks.GetByIdAsync(taskId, CancellationToken.None);
        Assert.Equal(FaqModuleTaskState.AwaitingQuestion, persisted!.State);
    }

    [Fact]
    public async Task AlreadyCompletedTask_ReturnsAlreadyComplete()
    {
        var tasks = new InMemoryFaqModuleTaskStore();
        var siteId = new SiteId(Guid.NewGuid());
        var taskId = new FaqModuleTaskId(Guid.NewGuid());
        var task = Domain.FaqModuleTask.Start(taskId, siteId, Now);
        task.Complete(Now);
        await tasks.AddAsync(task, CancellationToken.None);

        var handler = BuildHandler(
            tasks, new InMemoryKnowledgeBaseRepository(), new FakeFaqAnswerGenerator(new FaqAnswerResult.Answered("unused")));

        var result = await handler.HandleAsync(
            new ReplyToFaqModuleTask(taskId.Value.ToString(), Guid.NewGuid(), FaqStepKinds.Form, "another question"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("faq_module_task.already_complete", result.Error!.Value.Code);
    }

    [Fact]
    public async Task ValidReply_Answered_CompletesTask_AndReturnsAnswerStep()
    {
        var tasks = new InMemoryFaqModuleTaskStore();
        var siteId = new SiteId(Guid.NewGuid());
        var taskId = new FaqModuleTaskId(Guid.NewGuid());
        await tasks.AddAsync(Domain.FaqModuleTask.Start(taskId, siteId, Now), CancellationToken.None);

        var knowledgeBases = new InMemoryKnowledgeBaseRepository().Seed(siteId, "We accept returns within 30 days.", Now);
        var generator = new FakeFaqAnswerGenerator(new FaqAnswerResult.Answered("Within 30 days."));
        var handler = BuildHandler(tasks, knowledgeBases, generator);

        var result = await handler.HandleAsync(
            new ReplyToFaqModuleTask(taskId.Value.ToString(), Guid.NewGuid(), FaqStepKinds.Form, "what is your return policy?"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Complete);
        var answer = Assert.IsType<FaqModuleStep.Answer>(result.Value.Step);
        Assert.Equal("Within 30 days.", answer.Prompt);
        Assert.Equal("what is your return policy?", generator.LastRequest!.Question);

        var persisted = await tasks.GetByIdAsync(taskId, CancellationToken.None);
        Assert.Equal(FaqModuleTaskState.Completed, persisted!.State);
    }

    [Fact]
    public async Task ValidReply_NotGrounded_CompletesTask_AndReturnsEscalateStep()
    {
        var tasks = new InMemoryFaqModuleTaskStore();
        var siteId = new SiteId(Guid.NewGuid());
        var taskId = new FaqModuleTaskId(Guid.NewGuid());
        await tasks.AddAsync(Domain.FaqModuleTask.Start(taskId, siteId, Now), CancellationToken.None);

        var knowledgeBases = new InMemoryKnowledgeBaseRepository().Seed(siteId, "We accept returns within 30 days.", Now);
        var generator = new FakeFaqAnswerGenerator(new FaqAnswerResult.NotGrounded("Not covered."));
        var handler = BuildHandler(tasks, knowledgeBases, generator);

        var result = await handler.HandleAsync(
            new ReplyToFaqModuleTask(taskId.Value.ToString(), Guid.NewGuid(), FaqStepKinds.Form, "what is the airspeed of a swallow?"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Complete);
        Assert.IsType<FaqModuleStep.Escalate>(result.Value.Step);

        var persisted = await tasks.GetByIdAsync(taskId, CancellationToken.None);
        Assert.Equal(FaqModuleTaskState.Completed, persisted!.State);
    }
}
