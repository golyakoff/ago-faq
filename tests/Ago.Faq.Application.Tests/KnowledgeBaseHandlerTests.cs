using Ago.Faq.Application.UseCases.KnowledgeBase;
using Ago.Faq.Domain;

namespace Ago.Faq.Application.Tests;

public sealed class GetKnowledgeBaseHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NoRowForSite_ReturnsEmptyTextAndNullUpdatedAt()
    {
        var handler = new GetKnowledgeBaseHandler(new InMemoryKnowledgeBaseRepository());

        var result = await handler.HandleAsync(new GetKnowledgeBase(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Value.Text);
        Assert.Null(result.Value.UpdatedAt);
    }

    [Fact]
    public async Task ExistingRow_ReturnsItsTextAndUpdatedAt()
    {
        var siteId = Guid.NewGuid();
        var repository = new InMemoryKnowledgeBaseRepository().Seed(new SiteId(siteId), "We are open 9-6.", Now);
        var handler = new GetKnowledgeBaseHandler(repository);

        var result = await handler.HandleAsync(new GetKnowledgeBase(siteId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("We are open 9-6.", result.Value.Text);
        Assert.Equal(Now, result.Value.UpdatedAt);
    }
}

public sealed class PutKnowledgeBaseHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NewSite_CreatesRow_AndReturnsItsText()
    {
        var siteId = Guid.NewGuid();
        var repository = new InMemoryKnowledgeBaseRepository();
        var handler = new PutKnowledgeBaseHandler(repository, new FakeClock(Now));

        var result = await handler.HandleAsync(new PutKnowledgeBase(siteId, "We ship worldwide."), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("We ship worldwide.", result.Value.Text);
        Assert.Equal(Now, result.Value.UpdatedAt);

        var stored = await repository.FindBySiteIdAsync(new SiteId(siteId), CancellationToken.None);
        Assert.Equal("We ship worldwide.", stored!.Text);
    }

    [Fact]
    public async Task ExistingSite_UpdatesText_AndBumpsUpdatedAt()
    {
        var siteId = Guid.NewGuid();
        var repository = new InMemoryKnowledgeBaseRepository().Seed(new SiteId(siteId), "old text", Now);
        var later = Now.AddHours(1);
        var handler = new PutKnowledgeBaseHandler(repository, new FakeClock(later));

        var result = await handler.HandleAsync(new PutKnowledgeBase(siteId, "new text"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new text", result.Value.Text);
        Assert.Equal(later, result.Value.UpdatedAt);
    }

    [Fact]
    public async Task TextExceedingMaxLength_ReturnsTextTooLong_AndNeverSaves()
    {
        var siteId = Guid.NewGuid();
        var repository = new InMemoryKnowledgeBaseRepository();
        var handler = new PutKnowledgeBaseHandler(repository, new FakeClock(Now));
        var tooLong = new string('a', Domain.KnowledgeBase.MaxTextLength + 1);

        var result = await handler.HandleAsync(new PutKnowledgeBase(siteId, tooLong), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("knowledge_base.text_too_long", result.Error!.Value.Code);

        var stored = await repository.FindBySiteIdAsync(new SiteId(siteId), CancellationToken.None);
        Assert.Null(stored);
    }
}
