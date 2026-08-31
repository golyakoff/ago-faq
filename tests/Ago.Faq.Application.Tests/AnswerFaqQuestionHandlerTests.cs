using Ago.Faq.Application.Abstractions;
using Ago.Faq.Application.UseCases.FaqModuleTask;
using Ago.Faq.Domain;

namespace Ago.Faq.Application.Tests;

/// <summary>
/// The must-test rule from `19-03`'s own brief: if the site's knowledge-base text is null, empty, or
/// whitespace, <see cref="AnswerFaqQuestionHandler"/> returns the escalation without ever calling
/// <see cref="IFaqAnswerGenerator"/> - mirroring `ago-chat`'s own `19-02` "no tag vocabulary configured
/// -&gt; do nothing, never call the provider" rule. Proven here by asserting
/// <see cref="FakeFaqAnswerGenerator.CallCount"/> is zero, not merely by inspecting the returned step.
/// </summary>
public sealed class AnswerFaqQuestionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NoKnowledgeBaseRowAtAll_ReturnsEscalate_NeverCallsGenerator()
    {
        var generator = new FakeFaqAnswerGenerator(new FaqAnswerResult.Answered("should never be seen"));
        var handler = new AnswerFaqQuestionHandler(new InMemoryKnowledgeBaseRepository(), generator);

        var step = await handler.HandleAsync(new SiteId(Guid.NewGuid()), "do you ship to Kazan?", CancellationToken.None);

        Assert.IsType<FaqModuleStep.Escalate>(step);
        Assert.Equal(0, generator.CallCount);
    }

    [Fact]
    public async Task EmptyKnowledgeBaseText_ReturnsEscalate_NeverCallsGenerator()
    {
        var siteId = new SiteId(Guid.NewGuid());
        var repository = new InMemoryKnowledgeBaseRepository().Seed(siteId, string.Empty, Now);
        var generator = new FakeFaqAnswerGenerator(new FaqAnswerResult.Answered("should never be seen"));
        var handler = new AnswerFaqQuestionHandler(repository, generator);

        var step = await handler.HandleAsync(siteId, "what are your hours?", CancellationToken.None);

        Assert.IsType<FaqModuleStep.Escalate>(step);
        Assert.Equal(0, generator.CallCount);
    }

    [Fact]
    public async Task WhitespaceOnlyKnowledgeBaseText_ReturnsEscalate_NeverCallsGenerator()
    {
        var siteId = new SiteId(Guid.NewGuid());
        var repository = new InMemoryKnowledgeBaseRepository().Seed(siteId, "   \n\t  ", Now);
        var generator = new FakeFaqAnswerGenerator(new FaqAnswerResult.Answered("should never be seen"));
        var handler = new AnswerFaqQuestionHandler(repository, generator);

        var step = await handler.HandleAsync(siteId, "what are your hours?", CancellationToken.None);

        Assert.IsType<FaqModuleStep.Escalate>(step);
        Assert.Equal(0, generator.CallCount);
    }

    [Fact]
    public async Task NonEmptyKnowledgeBase_Answered_ReturnsAnswerStep_WithGeneratorsText()
    {
        var siteId = new SiteId(Guid.NewGuid());
        var repository = new InMemoryKnowledgeBaseRepository().Seed(siteId, "We ship worldwide.", Now);
        var generator = new FakeFaqAnswerGenerator(new FaqAnswerResult.Answered("Yes, we ship worldwide."));
        var handler = new AnswerFaqQuestionHandler(repository, generator);

        var step = await handler.HandleAsync(siteId, "do you ship internationally?", CancellationToken.None);

        var answer = Assert.IsType<FaqModuleStep.Answer>(step);
        Assert.Equal("Yes, we ship worldwide.", answer.Prompt);
        Assert.Equal(1, generator.CallCount);
        Assert.Equal("We ship worldwide.", generator.LastRequest!.KnowledgeBaseText);
        Assert.Equal("do you ship internationally?", generator.LastRequest!.Question);
    }

    [Fact]
    public async Task NonEmptyKnowledgeBase_NotGrounded_ReturnsEscalateWithReason()
    {
        var siteId = new SiteId(Guid.NewGuid());
        var repository = new InMemoryKnowledgeBaseRepository().Seed(siteId, "We ship worldwide.", Now);
        var generator = new FakeFaqAnswerGenerator(new FaqAnswerResult.NotGrounded("The knowledge base does not cover this."));
        var handler = new AnswerFaqQuestionHandler(repository, generator);

        var step = await handler.HandleAsync(siteId, "what is the meaning of life?", CancellationToken.None);

        var escalate = Assert.IsType<FaqModuleStep.Escalate>(step);
        Assert.Equal("The knowledge base does not cover this.", escalate.Prompt);
    }

    [Fact]
    public async Task NonEmptyKnowledgeBase_Unavailable_ReturnsEscalateWithReason()
    {
        var siteId = new SiteId(Guid.NewGuid());
        var repository = new InMemoryKnowledgeBaseRepository().Seed(siteId, "We ship worldwide.", Now);
        var generator = new FakeFaqAnswerGenerator(new FaqAnswerResult.Unavailable("The provider is down."));
        var handler = new AnswerFaqQuestionHandler(repository, generator);

        var step = await handler.HandleAsync(siteId, "do you ship to Mars?", CancellationToken.None);

        var escalate = Assert.IsType<FaqModuleStep.Escalate>(step);
        Assert.Equal("The provider is down.", escalate.Prompt);
    }

    [Fact]
    public async Task BlankReasonFromGenerator_FallsBackToAFriendlyDefault()
    {
        var siteId = new SiteId(Guid.NewGuid());
        var repository = new InMemoryKnowledgeBaseRepository().Seed(siteId, "We ship worldwide.", Now);
        var generator = new FakeFaqAnswerGenerator(new FaqAnswerResult.Unavailable(string.Empty));
        var handler = new AnswerFaqQuestionHandler(repository, generator);

        var step = await handler.HandleAsync(siteId, "anything?", CancellationToken.None);

        var escalate = Assert.IsType<FaqModuleStep.Escalate>(step);
        Assert.False(string.IsNullOrWhiteSpace(escalate.Prompt));
    }
}
