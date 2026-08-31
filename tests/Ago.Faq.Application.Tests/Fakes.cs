using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;
using Ago.Platform.Kernel;

namespace Ago.Faq.Application.Tests;

/// <summary>A fixed instant, settable per test - the same shape every fake clock in this codebase's
/// sibling repositories uses (`testing.md`, ago-root).</summary>
public sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

/// <summary>Deterministic, not random - a test that asserts on a returned id needs to know it in
/// advance.</summary>
public sealed class FakeIdGenerator : IIdGenerator
{
    private readonly Queue<Guid> _ids = new();

    public FakeIdGenerator EnqueueNext(Guid id)
    {
        _ids.Enqueue(id);
        return this;
    }

    public Guid NewId(DateTimeOffset now) => _ids.Count > 0 ? _ids.Dequeue() : Guid.NewGuid();
}

/// <summary>An in-memory <see cref="IFaqModuleTaskStore"/> - a plain dictionary keyed by id, the same
/// role a hand-rolled in-memory fake plays for every store-shaped port in this codebase's sibling
/// repositories (testing.md's "application-layer tests use fakes, not mocks" guidance).</summary>
public sealed class InMemoryFaqModuleTaskStore : IFaqModuleTaskStore
{
    private readonly Dictionary<FaqModuleTaskId, FaqModuleTask> _tasks = [];

    public Task<FaqModuleTask?> GetByIdAsync(FaqModuleTaskId id, CancellationToken cancellationToken) =>
        Task.FromResult(_tasks.GetValueOrDefault(id));

    public Task AddAsync(FaqModuleTask task, CancellationToken cancellationToken)
    {
        _tasks[task.Id] = task;
        return Task.CompletedTask;
    }

    public Task SaveAsync(FaqModuleTask task, CancellationToken cancellationToken)
    {
        _tasks[task.Id] = task;
        return Task.CompletedTask;
    }

    public bool Contains(FaqModuleTaskId id) => _tasks.ContainsKey(id);
}

public sealed class InMemoryKnowledgeBaseRepository : IKnowledgeBaseRepository
{
    private readonly Dictionary<SiteId, KnowledgeBase> _rows = [];

    public Task<KnowledgeBase?> FindBySiteIdAsync(SiteId siteId, CancellationToken cancellationToken) =>
        Task.FromResult(_rows.GetValueOrDefault(siteId));

    public Task UpsertAsync(KnowledgeBase knowledgeBase, CancellationToken cancellationToken)
    {
        _rows[knowledgeBase.SiteId] = knowledgeBase;
        return Task.CompletedTask;
    }

    public InMemoryKnowledgeBaseRepository Seed(SiteId siteId, string text, DateTimeOffset now)
    {
        _rows[siteId] = KnowledgeBase.Create(siteId, text, now);
        return this;
    }
}

/// <summary>Records whether it was ever called - the load-bearing assertion for the "empty knowledge
/// base never calls the provider" rule (<c>AnswerFaqQuestionHandlerTests</c>).</summary>
public sealed class FakeFaqAnswerGenerator(FaqAnswerResult result) : IFaqAnswerGenerator
{
    public int CallCount { get; private set; }

    public FaqAnswerRequest? LastRequest { get; private set; }

    public Task<FaqAnswerResult> AnswerAsync(FaqAnswerRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        return Task.FromResult(result);
    }
}
