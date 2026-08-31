using Ago.Platform.Kernel;

namespace Ago.Faq.Domain;

/// <summary>
/// A strongly-typed wrapper over the <c>siteId</c> Chat's wire contract carries
/// (<c>Ago.Chat.Infrastructure.Modules.ModuleWireContract.StartTaskWireRequest</c>) - opaque to this
/// module in every sense except one: it is the key this module's own <see cref="KnowledgeBase"/> rows
/// are stored under, because "which site's knowledge base answers this question" is a decision this
/// module has to make and Chat's <c>siteId</c> is the only correlation key it is ever given for it.
///
/// <para>A bare <see cref="Guid"/> would compile everywhere a <see cref="SiteId"/> or a
/// <see cref="FaqModuleTaskId"/> is expected, and the two are never interchangeable - the same
/// argument <c>Ago.Calendar.Domain.TenantId</c>'s own remarks make, restated here rather than shared
/// because each product's id types are its own (coding-style.md).</para>
/// </summary>
public readonly record struct SiteId(Guid Value) : IStronglyTypedId;
