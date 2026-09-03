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
        Assert.Null(registration.PreviousCredential);
        Assert.Null(registration.PreviousCredentialExpiresAt);
    }

    /// <summary>`22-11`'s own "no downtime for the site being rotated" claim, at the domain level.</summary>
    [Fact]
    public void ActiveCredentials_ImmediatelyAfterRotation_StillAcceptsTheOldCredential()
    {
        var original = new ModuleCredential("original-secret-of-sixteen-plus-chars");
        var rotated = new ModuleCredential("rotated-secret-of-sixteen-plus-chars-x");
        var registration = ModuleSiteRegistration.Register(new SiteId(Guid.NewGuid()), original, Now)
            .Rotate(rotated, Now, TimeSpan.FromMinutes(10));

        var active = registration.ActiveCredentials(Now).ToList();

        Assert.Contains(rotated, active);
        Assert.Contains(original, active);
    }

    [Fact]
    public void ActiveCredentials_AfterTheOverlapWindowExpires_NoLongerAcceptsTheOldCredential()
    {
        var original = new ModuleCredential("original-secret-of-sixteen-plus-chars");
        var rotated = new ModuleCredential("rotated-secret-of-sixteen-plus-chars-x");
        var overlap = TimeSpan.FromMinutes(10);
        var registration = ModuleSiteRegistration.Register(new SiteId(Guid.NewGuid()), original, Now)
            .Rotate(rotated, Now, overlap);

        var active = registration.ActiveCredentials(Now + overlap + TimeSpan.FromSeconds(1)).ToList();

        Assert.Contains(rotated, active);
        Assert.DoesNotContain(original, active);
    }

    [Fact]
    public void Rotate_SetsTheNewCredentialAsCurrent_AndPreservesRegisteredAt()
    {
        var original = new ModuleCredential("original-secret-of-sixteen-plus-chars");
        var rotated = new ModuleCredential("rotated-secret-of-sixteen-plus-chars-x");
        var registeredAt = Now.AddDays(-3);
        var registration = ModuleSiteRegistration.Register(new SiteId(Guid.NewGuid()), original, registeredAt);

        var result = registration.Rotate(rotated, Now, TimeSpan.FromMinutes(10));

        Assert.Equal(rotated, result.Credential);
        Assert.Equal(original, result.PreviousCredential);
        Assert.Equal(Now.AddMinutes(10), result.PreviousCredentialExpiresAt);
        Assert.Equal(registeredAt, result.RegisteredAt);
    }

    [Fact]
    public void Rotate_TwiceInARow_KeepsOnlyTheMostRecentPreviousCredential_NotTheOneBeforeThat()
    {
        var first = new ModuleCredential("first-secret-of-sixteen-plus-characters");
        var second = new ModuleCredential("second-secret-of-sixteen-plus-character");
        var third = new ModuleCredential("third-secret-of-sixteen-plus-characters");
        var registration = ModuleSiteRegistration.Register(new SiteId(Guid.NewGuid()), first, Now)
            .Rotate(second, Now, TimeSpan.FromMinutes(10));

        var result = registration.Rotate(third, Now, TimeSpan.FromMinutes(10));

        var active = result.ActiveCredentials(Now).ToList();
        Assert.Contains(third, active);
        Assert.Contains(second, active);
        Assert.DoesNotContain(first, active);
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
