using Ago.Faq.Domain;

namespace Ago.Faq.Domain.Tests;

public sealed class FaqModuleTaskTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_BeginsInAwaitingQuestion()
    {
        var task = FaqModuleTask.Start(new FaqModuleTaskId(Guid.NewGuid()), new SiteId(Guid.NewGuid()), Now);

        Assert.Equal(FaqModuleTaskState.AwaitingQuestion, task.State);
        Assert.Equal(Now, task.CreatedAt);
        Assert.Equal(Now, task.UpdatedAt);
    }

    [Fact]
    public void Complete_FromAwaitingQuestion_TransitionsToCompleted()
    {
        var task = FaqModuleTask.Start(new FaqModuleTaskId(Guid.NewGuid()), new SiteId(Guid.NewGuid()), Now);
        var later = Now.AddSeconds(5);

        task.Complete(later);

        Assert.Equal(FaqModuleTaskState.Completed, task.State);
        Assert.Equal(later, task.UpdatedAt);
        // CreatedAt never changes on a state transition.
        Assert.Equal(Now, task.CreatedAt);
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_ThrowsInvalidFaqModuleTaskStateException()
    {
        var task = FaqModuleTask.Start(new FaqModuleTaskId(Guid.NewGuid()), new SiteId(Guid.NewGuid()), Now);
        task.Complete(Now);

        Assert.Throws<InvalidFaqModuleTaskStateException>(() => task.Complete(Now));
    }
}
