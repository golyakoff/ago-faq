using Ago.Platform.Kernel;

namespace Ago.Faq.Application.UseCases.ModuleRegistration;

/// <summary>`22-11`: this surface's own expected failures - the same vocabulary
/// <c>Ago.Calendar.Application.UseCases.ChatModuleRegistration.ChatModuleRegistrationErrors</c>
/// establishes for its sibling. No <c>SiteNotFound</c> case: unlike Calendar, this module has no local
/// tenant table to check a site id against (adr/0093's own remarks - this module has "no tenants, no
/// operators, no roles") - any site id `Ago.Chat.*` names is a site this module is willing to
/// register for.</summary>
public static class ModuleRegistrationErrors
{
    public static Error AlreadyRegistered() => new(
        "module_registration.already_registered",
        "This site already has a module registration - rotate it instead of registering again.");

    public static Error NotFound() => new(
        "module_registration.not_found",
        "No module registration exists for that site.");

    public static Error InvalidCredential(string reason) => new("module_registration.invalid_credential", reason);
}
