namespace Ago.Faq.Domain.Tests;

public sealed class ModuleSiteRegistrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_SetsSiteIdCredentialAndRegisteredAt()
    {
        var siteId = new SiteId(Guid.NewGuid());
        var credential = new ModuleCredential("a-sufficiently-long-secret-value");

        var registration = ModuleSiteRegistration.Register(siteId, credential, Now);

        Assert.Equal(siteId, registration.SiteId);
        Assert.Equal(credential, registration.Credential);
        Assert.Equal(Now, registration.RegisteredAt);
    }
}

public sealed class ModuleCredentialTests
{
    [Fact]
    public void Constructor_TooShort_ThrowsArgumentException()
    {
        var tooShort = new string('a', ModuleCredential.MinLength - 1);

        Assert.Throws<ArgumentException>(() => new ModuleCredential(tooShort));
    }

    [Fact]
    public void Constructor_TooLong_ThrowsArgumentException()
    {
        var tooLong = new string('a', ModuleCredential.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => new ModuleCredential(tooLong));
    }

    [Fact]
    public void Constructor_Empty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ModuleCredential(string.Empty));
    }

    [Fact]
    public void Constructor_AtExactBounds_IsAllowed()
    {
        var atMin = new string('a', ModuleCredential.MinLength);
        var atMax = new string('a', ModuleCredential.MaxLength);

        Assert.Equal(atMin, new ModuleCredential(atMin).Value);
        Assert.Equal(atMax, new ModuleCredential(atMax).Value);
    }

    [Fact]
    public void ToString_NeverPrintsTheSecret()
    {
        var credential = new ModuleCredential("a-sufficiently-long-secret-value");

        Assert.DoesNotContain("sufficiently-long-secret-value", credential.ToString(), StringComparison.Ordinal);
    }
}
