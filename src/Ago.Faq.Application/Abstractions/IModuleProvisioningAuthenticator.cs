namespace Ago.Faq.Application.Abstractions;

/// <summary>
/// `22-11`: proves a call to `/api/v1/module-registrations*` is really from `Ago.Chat.*`'s own
/// deployment - the identical port `Ago.Calendar.Application.Abstractions.IModuleProvisioningAuthenticator`
/// gives its sibling, restated here rather than referenced (each product's own domain/application types
/// are its own, coding-style.md). See that interface's own remarks for the full argument: a plain
/// shared-secret compare, deliberately not adr/0094's signed-assertion format, because provisioning is
/// a low-volume admin-to-admin channel with a different threat model from the per-message channel that
/// format was built for.
/// </summary>
public interface IModuleProvisioningAuthenticator
{
    bool Authenticate(string? headerValue);
}
