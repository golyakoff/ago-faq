using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;
using Ago.Platform.Kernel;

namespace Ago.Faq.Application.UseCases.ModuleRegistration;

/// <summary>`22-11`'s own "rotate without downtime" Done-when - the identical mechanism and the
/// identical ten-minute, implementer's-call overlap window
/// <c>Ago.Calendar.Application.UseCases.ChatModuleRegistration.RotateChatModuleCredentialHandler</c>'s
/// own remarks give for its sibling, and the identical module-first-then-caller-second ordering
/// argument.</summary>
public sealed class RotateModuleSiteCredentialHandler(IModuleSiteRegistrationRepository registrations, IClock clock)
{
    private static readonly TimeSpan OverlapWindow = TimeSpan.FromMinutes(10);

    public async Task<Result> HandleAsync(RotateModuleSiteCredential command, CancellationToken cancellationToken)
    {
        var siteId = new SiteId(command.SiteId);

        var existing = await registrations.GetBySiteIdAsync(siteId, cancellationToken);
        if (existing is null)
        {
            return ModuleRegistrationErrors.NotFound();
        }

        ModuleCredential newCredential;
        try
        {
            newCredential = new ModuleCredential(command.NewCredential);
        }
        catch (ArgumentException ex)
        {
            return ModuleRegistrationErrors.InvalidCredential(ex.Message);
        }

        var rotated = existing.Rotate(newCredential, clock.UtcNow, OverlapWindow);
        await registrations.UpdateAsync(rotated, cancellationToken);
        return Result.Success();
    }
}
