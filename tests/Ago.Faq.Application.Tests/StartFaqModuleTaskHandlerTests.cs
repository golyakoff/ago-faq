using Ago.Faq.Application.Abstractions;
using Ago.Faq.Application.UseCases.FaqModuleTask;
using Ago.Faq.Domain;

namespace Ago.Faq.Application.Tests;

public sealed class StartFaqModuleTaskHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    private static StartFaqModuleTaskHandler BuildHandler(
        InMemoryFaqModuleTaskStore tasks, InMemoryKnowledgeBaseRepository knowledgeBases, FakeFaqAnswerGenerator generator, Guid taskId)
    {
        var answerHandler = new AnswerFaqQuestionHandler(knowledgeBases, generator);
        return new StartFaqModuleTaskHandler(
            tasks, answerHandler, new FakeIdGenerator().EnqueueNext(taskId), new FakeClock(Now));
    }

    [Theory]
    [InlineData("/faq")]
    [InlineData("/faq ")]
    [InlineData("/faq   ")]
    [InlineData("/faq\t")]
    public async Task BareTrigger_ReturnsFormStep_NotComplete_AndPersistsAwaitingQuestion(string triggerText)
    {
        var tasks = new InMemoryFaqModuleTaskStore();
        var generator = new FakeFaqAnswerGenerator(new FaqAnswerResult.Answered("unused"));
        var taskId = Guid.NewGuid();
        var handler = BuildHandler(tasks, new InMemoryKnowledgeBaseRepository(), generator, taskId);

        var result = await handler.HandleAsync(
            new StartFaqModuleTask(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), triggerText), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var started = result.Value;
        Assert.False(started.Complete);
        var form = Assert.IsType<FaqModuleStep.Form>(started.Step);
        Assert.Equal("question", form.FieldId);
        Assert.Equal(taskId.ToString(), started.ExternalTaskId);

        var persisted = await tasks.GetByIdAsync(new FaqModuleTaskId(taskId), CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(FaqModuleTaskState.AwaitingQuestion, persisted!.State);
        Assert.Equal(0, generator.CallCount);
    }

    [Fact]
    public async Task InlineQuestion_KnowledgeBaseAnswers_ReturnsAnswerStep_Complete_AndPersistsCompleted()
    {
        var siteId = Guid.NewGuid();
        var tasks = new InMemoryFaqModuleTaskStore();
        var knowledgeBases = new InMemoryKnowledgeBaseRepository().Seed(new SiteId(siteId), "We ship worldwide.", Now);
        var generator = new FakeFaqAnswerGenerator(new FaqAnswerResult.Answered("Yes, we ship worldwide."));
        var taskId = Guid.NewGuid();
        var handler = BuildHandler(tasks, knowledgeBases, generator, taskId);

        var result = await handler.HandleAsync(
            new StartFaqModuleTask(Guid.NewGuid(), siteId, Guid.NewGuid(), "/faq do you ship internationally?"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var started = result.Value;
        Assert.True(started.Complete);
        var answer = Assert.IsType<FaqModuleStep.Answer>(started.Step);
        Assert.Equal("Yes, we ship worldwide.", answer.Prompt);
        Assert.Equal("do you ship internationally?", generator.LastRequest!.Question);

        var persisted = await tasks.GetByIdAsync(new FaqModuleTaskId(taskId), CancellationToken.None);
        Assert.Equal(FaqModuleTaskState.Completed, persisted!.State);
    }

    [Fact]
    public async Task InlineQuestion_NoKnowledgeBaseConfigured_ReturnsEscalate_Complete_NeverCallsGenerator()
    {
        var tasks = new InMemoryFaqModuleTaskStore();
        var generator = new FakeFaqAnswerGenerator(new FaqAnswerResult.Answered("unused"));
        var taskId = Guid.NewGuid();
        var handler = BuildHandler(tasks, new InMemoryKnowledgeBaseRepository(), generator, taskId);

        var result = await handler.HandleAsync(
            new StartFaqModuleTask(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "/faq what is your return policy?"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var started = result.Value;
        Assert.True(started.Complete);
        Assert.IsType<FaqModuleStep.Escalate>(started.Step);
        Assert.Equal(0, generator.CallCount);
    }

    [Fact]
    public async Task DifferentTriggerWord_StillStripsFirstToken()
    {
        var tasks = new InMemoryFaqModuleTaskStore();
        var siteId = Guid.NewGuid();
        var knowledgeBases = new InMemoryKnowledgeBaseRepository().Seed(new SiteId(siteId), "We are open 9-6.", Now);
        var generator = new FakeFaqAnswerGenerator(new FaqAnswerResult.Answered("9 to 6."));
        var handler = BuildHandler(tasks, knowledgeBases, generator, Guid.NewGuid());

        var result = await handler.HandleAsync(
            new StartFaqModuleTask(Guid.NewGuid(), siteId, Guid.NewGuid(), "/помощь what are your hours?"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("what are your hours?", generator.LastRequest!.Question);
    }
}
