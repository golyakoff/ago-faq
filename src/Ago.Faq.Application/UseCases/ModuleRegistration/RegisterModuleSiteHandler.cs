using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;
using Ago.Platform.Kernel;

namespace Ago.Faq.Application.UseCases.ModuleRegistration;

/// <summary>
/// `22-11`: registers "site X's chat-originated calls are proven by this credential" - the write this
/// item's own backlog item found missing entirely. Called synchronously from `Ago.Chat.*`'s own
/// `EnableModuleForSiteHandler`, before that handler persists its own `EnabledModule` row - see
/// <c>Ago.Calendar.Application.UseCases.ChatModuleRegistration.RegisterChatModuleHandler</c>'s own
/// remarks for the full argument against an outbox-mediated, eventually-consistent alternative
/// (identical here: rotation's own "no downtime" claim needs this module to confirm a credential
/// change before `Ago.Chat.*` starts minting calls with it).
///
/// <para><b>No tenant-existence check, unlike Calendar's sibling.</b> This module has no local account
/// table to validate a site id against - adr/0093's own remarks name this module as having "no
/// tenants, no operators, no roles" by design. Any syntactically valid site id `Ago.Chat.*` presents is
/// one this module is willing to register for; the only refusal this handler has is
/// "already registered".</para>
/// </summary>
public sealed class RegisterModuleSiteHandler(IModuleSiteRegistrationRepository registrations, IClock clock)
{
    public async Task<Result> HandleAsync(RegisterModuleSite command, CancellationToken cancellationToken)
    {
        var siteId = new SiteId(command.SiteId);

        var existing = await registrations.GetBySiteIdAsync(siteId, cancellationToken);
        if (existing is not null)
        {
            return ModuleRegistrationErrors.AlreadyRegistered();
        }

        ModuleCredential credential;
        try
        {
            credential = new ModuleCredential(command.Credential);
        }
        catch (ArgumentException ex)
        {
            return ModuleRegistrationErrors.InvalidCredential(ex.Message);
        }

        var registration = Domain.ModuleSiteRegistration.Register(siteId, credential, clock.UtcNow);
        await registrations.AddAsync(registration, cancellationToken);
        return Result.Success();
    }
}
