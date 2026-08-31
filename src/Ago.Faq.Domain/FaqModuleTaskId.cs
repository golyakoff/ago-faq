using Ago.Platform.Kernel;

namespace Ago.Faq.Domain;

/// <summary>
/// This aggregate's own id, and also the wire's <c>externalTaskId</c> in string form - the identical
/// "no separate correlation column" call <c>Ago.Calendar.Domain.ChatBookingTaskId</c>'s own remarks
/// make: nothing but this aggregate ever mints one, so there is nothing to correlate against besides
/// itself.
/// </summary>
public readonly record struct FaqModuleTaskId(Guid Value) : IStronglyTypedId;
