using Ago.Faq.Application.UseCases.ModuleRegistration;
using Ago.Faq.Domain;

namespace Ago.Faq.Application.Tests.UseCases.ModuleRegistration;

/// <summary>`22-11`'s own first Done-when, at the Application level.</summary>
public sealed class RegisterModuleSiteHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly SiteId SiteId = new(Guid.NewGuid());
    private const string ValidCredential = "a-shared-secret-of-sixteen-plus-chars";

    [Fact]
    public async Task HandleAsync_ForASiteWithNoExistingRow_CreatesTheRegistration()
    {
        var registrations = new FakeModuleSiteRegistrationRepository();
        var handler = new RegisterModuleSiteHandler(registrations, new FakeClock(Now));

        var result = await handler.HandleAsync(new RegisterModuleSite(SiteId.Value, ValidCredential), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = await registrations.GetBySiteIdAsync(SiteId, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal(new ModuleCredential(ValidCredential), saved!.Credential);
        Assert.Equal(Now, saved.RegisteredAt);
    }

    [Fact]
    public async Task HandleAsync_ForASiteAlreadyRegistered_ReturnsAlreadyRegistered_AndDoesNotOverwrite()
    {
        var registrations = new FakeModuleSiteRegistrationRepository();
        await registrations.AddAsync(
            Domain.ModuleSiteRegistration.Register(SiteId, new ModuleCredential(ValidCredential), Now), CancellationToken.None);
        var handler = new RegisterModuleSiteHandler(registrations, new FakeClock(Now));

        var result = await handler.HandleAsync(
            new RegisterModuleSite(SiteId.Value, "a-completely-different-secret-value"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("module_registration.already_registered", result.Error!.Value.Code);
        var stillThere = await registrations.GetBySiteIdAsync(SiteId, CancellationToken.None);
        Assert.Equal(new ModuleCredential(ValidCredential), stillThere!.Credential);
    }

    [Fact]
    public async Task HandleAsync_WithATooShortCredential_ReturnsInvalidCredential()
    {
        var registrations = new FakeModuleSiteRegistrationRepository();
        var handler = new RegisterModuleSiteHandler(registrations, new FakeClock(Now));

        var result = await handler.HandleAsync(new RegisterModuleSite(SiteId.Value, "too-short"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("module_registration.invalid_credential", result.Error!.Value.Code);
        Assert.Null(await registrations.GetBySiteIdAsync(SiteId, CancellationToken.None));
    }
}
