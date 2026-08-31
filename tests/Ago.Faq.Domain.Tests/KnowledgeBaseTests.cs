namespace Ago.Faq.Domain.Tests;

public sealed class KnowledgeBaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithinLimit_SetsTextAndUpdatedAt()
    {
        var siteId = new SiteId(Guid.NewGuid());

        var kb = KnowledgeBase.Create(siteId, "We ship worldwide.", Now);

        Assert.Equal(siteId, kb.SiteId);
        Assert.Equal("We ship worldwide.", kb.Text);
        Assert.Equal(Now, kb.UpdatedAt);
    }

    [Fact]
    public void Create_EmptyText_IsAllowed()
    {
        var kb = KnowledgeBase.Create(new SiteId(Guid.NewGuid()), string.Empty, Now);

        Assert.Equal(string.Empty, kb.Text);
    }

    [Fact]
    public void Create_TextExceedingMaxLength_ThrowsArgumentException()
    {
        var tooLong = new string('a', KnowledgeBase.MaxTextLength + 1);

        Assert.Throws<ArgumentException>(() => KnowledgeBase.Create(new SiteId(Guid.NewGuid()), tooLong, Now));
    }

    [Fact]
    public void Create_TextAtExactlyMaxLength_IsAllowed()
    {
        var exact = new string('a', KnowledgeBase.MaxTextLength);

        var kb = KnowledgeBase.Create(new SiteId(Guid.NewGuid()), exact, Now);

        Assert.Equal(KnowledgeBase.MaxTextLength, kb.Text.Length);
    }

    [Fact]
    public void UpdateText_ReplacesTextAndBumpsUpdatedAt()
    {
        var kb = KnowledgeBase.Create(new SiteId(Guid.NewGuid()), "old text", Now);
        var later = Now.AddMinutes(10);

        kb.UpdateText("new text", later);

        Assert.Equal("new text", kb.Text);
        Assert.Equal(later, kb.UpdatedAt);
    }

    [Fact]
    public void UpdateText_ExceedingMaxLength_ThrowsArgumentException_AndLeavesTextUnchanged()
    {
        var kb = KnowledgeBase.Create(new SiteId(Guid.NewGuid()), "old text", Now);
        var tooLong = new string('a', KnowledgeBase.MaxTextLength + 1);

        Assert.Throws<ArgumentException>(() => kb.UpdateText(tooLong, Now.AddMinutes(1)));
        Assert.Equal("old text", kb.Text);
    }
}
