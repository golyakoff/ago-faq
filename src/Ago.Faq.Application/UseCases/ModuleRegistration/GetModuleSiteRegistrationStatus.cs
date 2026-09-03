namespace Ago.Faq.Application.UseCases.ModuleRegistration;

/// <summary>`22-11`'s own fourth Done-when: this module's own half of "a registration that exists on
/// one side only is detectable" - see
/// <c>Ago.Calendar.Application.UseCases.ChatModuleRegistration.GetChatModuleRegistrationStatus</c>'s
/// own remarks for its sibling.</summary>
public sealed record GetModuleSiteRegistrationStatus(Guid SiteId);

public readonly record struct ModuleSiteRegistrationStatus(
    bool Exists, DateTimeOffset RegisteredAt, bool HasCredentialInGracePeriod);
