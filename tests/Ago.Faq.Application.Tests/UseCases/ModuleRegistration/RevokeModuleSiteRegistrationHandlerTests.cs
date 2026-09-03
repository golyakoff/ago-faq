using Ago.Faq.Application.UseCases.ModuleRegistration;
using Ago.Faq.Domain;

namespace Ago.Faq.Application.Tests.UseCases.ModuleRegistration;

/// <summary>`22-11`'s own third Done-when, at the Application level.</summary>
public sealed class RevokeModuleSiteRegistrationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly SiteId SiteId = new(Guid.NewGuid());
    private const string Credential = "a-shared-secret-of-sixteen-plus-chars";

    [Fact]
    public async Task HandleAsync_ForARegisteredSite_RemovesTheRow()
    {
        var registrations = new FakeModuleSiteRegistrationRepository();
        await registrations.AddAsync(
            Domain.ModuleSiteRegistration.Register(SiteId, new ModuleCredential(Credential), Now), CancellationToken.None);
        var handler = new RevokeModuleSiteRegistrationHandler(registrations);

        var result = await handler.HandleAsync(new RevokeModuleSiteRegistration(SiteId.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(await registrations.GetBySiteIdAsync(SiteId, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ForASiteWithNoRegistration_ReturnsNotFound()
    {
        var registrations = new FakeModuleSiteRegistrationRepository();
        var handler = new RevokeModuleSiteRegistrationHandler(registrations);

        var result = await handler.HandleAsync(new RevokeModuleSiteRegistration(SiteId.Value), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("module_registration.not_found", result.Error!.Value.Code);
    }
}
