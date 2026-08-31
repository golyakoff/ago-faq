using Ago.Faq.Domain;
using Ago.Faq.Infrastructure.Postgres;

namespace Ago.Faq.Integration.Tests;

[Collection(PostgresCollection.Name)]
public sealed class FaqModuleTaskStoreTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddThenGetById_RoundTripsEveryField()
    {
        await using var db = fixture.CreateDbContext();
        var store = new FaqModuleTaskStore(db);

        var task = FaqModuleTask.Start(new FaqModuleTaskId(Guid.NewGuid()), new SiteId(Guid.NewGuid()), Now);
        await store.AddAsync(task, CancellationToken.None);

        await using var readDb = fixture.CreateDbContext();
        var reloaded = await new FaqModuleTaskStore(readDb).GetByIdAsync(task.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(task.Id, reloaded!.Id);
        Assert.Equal(task.SiteId, reloaded.SiteId);
        Assert.Equal(FaqModuleTaskState.AwaitingQuestion, reloaded.State);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNull()
    {
        await using var db = fixture.CreateDbContext();
        var store = new FaqModuleTaskStore(db);

        var reloaded = await store.GetByIdAsync(new FaqModuleTaskId(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(reloaded);
    }

    [Fact]
    public async Task Save_AfterLoad_PersistsTheStateTransition()
    {
        var taskId = new FaqModuleTaskId(Guid.NewGuid());

        await using (var db = fixture.CreateDbContext())
        {
            var task = FaqModuleTask.Start(taskId, new SiteId(Guid.NewGuid()), Now);
            await new FaqModuleTaskStore(db).AddAsync(task, CancellationToken.None);
        }

        await using (var db = fixture.CreateDbContext())
        {
            var store = new FaqModuleTaskStore(db);
            var task = await store.GetByIdAsync(taskId, CancellationToken.None);
            task!.Complete(Now.AddMinutes(1));
            await store.SaveAsync(task, CancellationToken.None);
        }

        await using (var db = fixture.CreateDbContext())
        {
            var reloaded = await new FaqModuleTaskStore(db).GetByIdAsync(taskId, CancellationToken.None);
            Assert.Equal(FaqModuleTaskState.Completed, reloaded!.State);
        }
    }
}
