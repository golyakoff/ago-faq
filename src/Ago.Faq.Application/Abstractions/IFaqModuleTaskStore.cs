using Ago.Faq.Domain;

namespace Ago.Faq.Application.Abstractions;

/// <summary>
/// The write-side port for <see cref="FaqModuleTask"/>. A plain load/save pair, the same shape
/// <c>Ago.Calendar.Application.Abstractions.IChatBookingTaskStore</c> uses for the identical reason:
/// exactly one caller (the visitor on this one chat task) ever advances one row, one reply at a time,
/// so there is no compare-and-set to design here.
///
/// <para>Declared in Application, implemented in <c>Ago.Faq.Infrastructure.Postgres</c>
/// (clean-architecture.md's dependency rule) - Application must not know this is EF Core and Postgres
/// underneath; the alternative, injecting a <c>DbContext</c> directly into the handlers below, would
/// make them untestable without a real database.</para>
/// </summary>
public interface IFaqModuleTaskStore
{
    /// <summary>Looked up by its own id, which is also the wire's <c>externalTaskId</c> in string
    /// form - see <see cref="FaqModuleTask"/>'s own remarks.</summary>
    Task<FaqModuleTask?> GetByIdAsync(FaqModuleTaskId id, CancellationToken cancellationToken);

    Task AddAsync(FaqModuleTask task, CancellationToken cancellationToken);

    Task SaveAsync(FaqModuleTask task, CancellationToken cancellationToken);
}
