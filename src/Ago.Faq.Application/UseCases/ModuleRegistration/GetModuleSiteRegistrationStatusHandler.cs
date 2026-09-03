using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;
using Ago.Platform.Kernel;

namespace Ago.Faq.Application.UseCases.ModuleRegistration;

public sealed class GetModuleSiteRegistrationStatusHandler(IModuleSiteRegistrationRepository registrations, IClock clock)
{
    public async Task<ModuleSiteRegistrationStatus> HandleAsync(
        GetModuleSiteRegistrationStatus query, CancellationToken cancellationToken)
    {
        var registration = await registrations.GetBySiteIdAsync(new SiteId(query.SiteId), cancellationToken);
        if (registration is null)
        {
            return new ModuleSiteRegistrationStatus(Exists: false, default, HasCredentialInGracePeriod: false);
        }

        var hasGrace = registration.ActiveCredentials(clock.UtcNow).Count() > 1;
        return new ModuleSiteRegistrationStatus(Exists: true, registration.RegisteredAt, hasGrace);
    }
}
