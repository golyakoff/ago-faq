using Ago.Faq.Domain;
using Ago.Faq.Infrastructure.Postgres;

namespace Ago.Faq.Integration.Tests;

[Collection(PostgresCollection.Name)]
public sealed class KnowledgeBaseRepositoryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FindBySiteId_NoRow_ReturnsNull()
    {
        await using var db = fixture.CreateDbContext();
        var repository = new KnowledgeBaseRepository(db);

        var found = await repository.FindBySiteIdAsync(new SiteId(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(found);
    }

    [Fact]
    public async Task Upsert_NewRow_ThenFind_RoundTripsText()
    {
        var siteId = new SiteId(Guid.NewGuid());

        await using (var db = fixture.CreateDbContext())
        {
            var repository = new KnowledgeBaseRepository(db);
            var kb = KnowledgeBase.Create(siteId, "We ship worldwide.", Now);
            await repository.UpsertAsync(kb, CancellationToken.None);
        }

        await using (var db = fixture.CreateDbContext())
        {
            var found = await new KnowledgeBaseRepository(db).FindBySiteIdAsync(siteId, CancellationToken.None);
            Assert.NotNull(found);
            Assert.Equal("We ship worldwide.", found!.Text);
            Assert.Equal(Now, found.UpdatedAt);
        }
    }

    [Fact]
    public async Task Upsert_ExistingRowLoadedThenMutated_UpdatesInPlace()
    {
        var siteId = new SiteId(Guid.NewGuid());

        await using (var db = fixture.CreateDbContext())
        {
            await new KnowledgeBaseRepository(db).UpsertAsync(
                KnowledgeBase.Create(siteId, "old text", Now), CancellationToken.None);
        }

        var later = Now.AddHours(2);
        await using (var db = fixture.CreateDbContext())
        {
            var repository = new KnowledgeBaseRepository(db);
            var existing = await repository.FindBySiteIdAsync(siteId, CancellationToken.None);
            existing!.UpdateText("new text", later);
            await repository.UpsertAsync(existing, CancellationToken.None);
        }

        await using (var db = fixture.CreateDbContext())
        {
            var found = await new KnowledgeBaseRepository(db).FindBySiteIdAsync(siteId, CancellationToken.None);
            Assert.Equal("new text", found!.Text);
            Assert.Equal(later, found.UpdatedAt);
        }
    }
}
