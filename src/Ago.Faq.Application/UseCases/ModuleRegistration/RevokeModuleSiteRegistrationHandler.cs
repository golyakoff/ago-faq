using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;
using Ago.Platform.Kernel;

namespace Ago.Faq.Application.UseCases.ModuleRegistration;

/// <summary>`22-11`'s own "revoking refuses subsequent calls" Done-when - immediate, total deletion,
/// the identical reasoning
/// <c>Ago.Calendar.Application.UseCases.ChatModuleRegistration.RevokeChatModuleRegistrationHandler</c>'s
/// own remarks give for its sibling: no grace window, because revocation exists precisely for the
/// moment a secret should stop working as fast as possible.</summary>
public sealed class RevokeModuleSiteRegistrationHandler(IModuleSiteRegistrationRepository registrations)
{
    public async Task<Result> HandleAsync(RevokeModuleSiteRegistration command, CancellationToken cancellationToken)
    {
        var siteId = new SiteId(command.SiteId);

        var existing = await registrations.GetBySiteIdAsync(siteId, cancellationToken);
        if (existing is null)
        {
            return ModuleRegistrationErrors.NotFound();
        }

        await registrations.DeleteAsync(siteId, cancellationToken);
        return Result.Success();
    }
}
