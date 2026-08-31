using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;
using Ago.Faq.Infrastructure.Postgres.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ago.Faq.Infrastructure.Postgres;

/// <summary>
/// The adapter for <see cref="IFaqModuleTaskStore"/>. Plain EF load-mutate-save throughout - this
/// aggregate has no concurrent-write problem to solve (the port's own remarks), so there is no
/// compare-and-set to write the way <c>Ago.Calendar.Infrastructure.Postgres.BookingStore</c> has for
/// <c>Event</c>.
/// </summary>
public sealed class FaqModuleTaskStore(AgoFaqDbContext db) : IFaqModuleTaskStore
{
    public Task<FaqModuleTask?> GetByIdAsync(FaqModuleTaskId id, CancellationToken cancellationToken) =>
        db.FaqModuleTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task AddAsync(FaqModuleTask task, CancellationToken cancellationToken)
    {
        db.FaqModuleTasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(FaqModuleTask task, CancellationToken cancellationToken)
    {
        if (db.Entry(task).State == EntityState.Detached)
        {
            db.FaqModuleTasks.Update(task);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
