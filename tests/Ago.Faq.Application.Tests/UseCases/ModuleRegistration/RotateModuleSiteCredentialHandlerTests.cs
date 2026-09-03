using Ago.Faq.Application.UseCases.ModuleRegistration;
using Ago.Faq.Domain;

namespace Ago.Faq.Application.Tests.UseCases.ModuleRegistration;

/// <summary>`22-11`'s own second Done-when, at the Application level.</summary>
public sealed class RotateModuleSiteCredentialHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly SiteId SiteId = new(Guid.NewGuid());
    private const string OriginalCredential = "original-secret-of-sixteen-plus-chars";
    private const string NewCredential = "rotated-secret-of-sixteen-plus-chars-x";

    [Fact]
    public async Task HandleAsync_ForARegisteredSite_ReplacesTheCurrentCredential()
    {
        var registrations = new FakeModuleSiteRegistrationRepository();
        await registrations.AddAsync(
            Domain.ModuleSiteRegistration.Register(SiteId, new ModuleCredential(OriginalCredential), Now), CancellationToken.None);
        var handler = new RotateModuleSiteCredentialHandler(registrations, new FakeClock(Now));

        var result = await handler.HandleAsync(new RotateModuleSiteCredential(SiteId.Value, NewCredential), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = await registrations.GetBySiteIdAsync(SiteId, CancellationToken.None);
        Assert.Equal(new ModuleCredential(NewCredential), saved!.Credential);
    }

    [Fact]
    public async Task HandleAsync_TheOldCredentialStillVerifiesImmediatelyAfterRotation()
    {
        var registrations = new FakeModuleSiteRegistrationRepository();
        await registrations.AddAsync(
            Domain.ModuleSiteRegistration.Register(SiteId, new ModuleCredential(OriginalCredential), Now), CancellationToken.None);
        var clock = new FakeClock(Now);
        var handler = new RotateModuleSiteCredentialHandler(registrations, clock);

        await handler.HandleAsync(new RotateModuleSiteCredential(SiteId.Value, NewCredential), CancellationToken.None);

        var saved = await registrations.GetBySiteIdAsync(SiteId, CancellationToken.None);
        var active = saved!.ActiveCredentials(clock.UtcNow).ToList();
        Assert.Contains(new ModuleCredential(OriginalCredential), active);
        Assert.Contains(new ModuleCredential(NewCredential), active);
    }

    [Fact]
    public async Task HandleAsync_ForASiteWithNoRegistration_ReturnsNotFound()
    {
        var registrations = new FakeModuleSiteRegistrationRepository();
        var handler = new RotateModuleSiteCredentialHandler(registrations, new FakeClock(Now));

        var result = await handler.HandleAsync(new RotateModuleSiteCredential(SiteId.Value, NewCredential), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("module_registration.not_found", result.Error!.Value.Code);
    }

    [Fact]
    public async Task HandleAsync_WithATooShortNewCredential_ReturnsInvalidCredential_AndLeavesTheOldOneInPlace()
    {
        var registrations = new FakeModuleSiteRegistrationRepository();
        await registrations.AddAsync(
            Domain.ModuleSiteRegistration.Register(SiteId, new ModuleCredential(OriginalCredential), Now), CancellationToken.None);
        var handler = new RotateModuleSiteCredentialHandler(registrations, new FakeClock(Now));

        var result = await handler.HandleAsync(new RotateModuleSiteCredential(SiteId.Value, "too-short"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("module_registration.invalid_credential", result.Error!.Value.Code);
        var saved = await registrations.GetBySiteIdAsync(SiteId, CancellationToken.None);
        Assert.Equal(new ModuleCredential(OriginalCredential), saved!.Credential);
    }
}
