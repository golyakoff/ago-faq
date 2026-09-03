namespace Ago.Faq.Application.UseCases.ModuleRegistration;

/// <summary>`22-11`: the module-side half of `Ago.Chat.*`'s own `EnableModuleForSite` - see
/// <c>Ago.Calendar.Application.UseCases.ChatModuleRegistration.RegisterChatModule</c>'s own remarks for
/// why <see cref="SiteId"/> arrives already proven and this command's only job is the write.</summary>
public sealed record RegisterModuleSite(Guid SiteId, string Credential);
