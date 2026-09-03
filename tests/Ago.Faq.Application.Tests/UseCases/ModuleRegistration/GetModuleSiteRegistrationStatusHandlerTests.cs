using Ago.Faq.Application.UseCases.ModuleRegistration;
using Ago.Faq.Domain;

namespace Ago.Faq.Application.Tests.UseCases.ModuleRegistration;

/// <summary>`22-11`'s own fourth Done-when.</summary>
public sealed class GetModuleSiteRegistrationStatusHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly SiteId SiteId = new(Guid.NewGuid());
    private const string Credential = "a-shared-secret-of-sixteen-plus-chars";

    [Fact]
    public async Task HandleAsync_ForASiteWithNoRegistration_ReportsNotExists()
    {
        var registrations = new FakeModuleSiteRegistrationRepository();
        var handler = new GetModuleSiteRegistrationStatusHandler(registrations, new FakeClock(Now));

        var status = await handler.HandleAsync(new GetModuleSiteRegistrationStatus(SiteId.Value), CancellationToken.None);

        Assert.False(status.Exists);
    }

    [Fact]
    public async Task HandleAsync_ImmediatelyAfterARotation_ReportsAGracePeriodCredential()
    {
        var registrations = new FakeModuleSiteRegistrationRepository();
        var registration = Domain.ModuleSiteRegistration.Register(SiteId, new ModuleCredential(Credential), Now)
            .Rotate(new ModuleCredential("rotated-secret-of-sixteen-plus-chars-x"), Now, TimeSpan.FromMinutes(10));
        await registrations.AddAsync(registration, CancellationToken.None);
        var handler = new GetModuleSiteRegistrationStatusHandler(registrations, new FakeClock(Now));

        var status = await handler.HandleAsync(new GetModuleSiteRegistrationStatus(SiteId.Value), CancellationToken.None);

        Assert.True(status.Exists);
        Assert.True(status.HasCredentialInGracePeriod);
    }
}
