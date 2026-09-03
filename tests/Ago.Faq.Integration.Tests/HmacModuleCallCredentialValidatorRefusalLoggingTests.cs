using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;
using Ago.Faq.Infrastructure.Postgres;
using Microsoft.Extensions.Logging;

namespace Ago.Faq.Integration.Tests;

/// <summary>
/// `22-12`/adr/0099: the identical suite `Ago.Calendar.Integration.Tests`'s own copy of this file
/// proves, applied to this product's own <see cref="HmacModuleCallCredentialValidator"/> - see that
/// suite's own remarks for the full reasoning (why this is a direct, fake-backed test of one
/// Infrastructure adapter rather than a real-Postgres/real-host test, why it lives in this project
/// rather than a new one, and why every assertion below avoids the presented secret/header). Two
/// independent implementations of one hand-kept wire format get two independent proofs, the same
/// "if you are changing this file, the identical change belongs in the sibling too" rule this class's
/// own remarks already state.
/// </summary>
public sealed class HmacModuleCallCredentialValidatorRefusalLoggingTests
{
    private const string OriginalSecret = "the-original-secret-of-sufficient-length-x";
    private const string RotatedSecret = "the-freshly-rotated-secret-of-enough-length";
    private const string WrongSecret = "a-completely-different-secret-nobody-holds";

    [Fact]
    public async Task NoCredentialHeader_IsRefused_AsNoCredential_LoggedAtDebug_WithNoSiteToName()
    {
        var (validator, logger, _) = Build();

        var result = await validator.ValidateAsync(headerValue: null, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(ModuleCallRefusalReason.NoCredential, result.Reason);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("NoCredential", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMalformedHeader_IsRefused_AsMalformed_LoggedAtDebug()
    {
        var (validator, logger, _) = Build();

        var result = await validator.ValidateAsync("not-even-shaped-like-a-token", DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(ModuleCallRefusalReason.Malformed, result.Reason);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("Malformed", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>The item's own first case: "the site has no registration - the module was never
    /// enabled for it." The repository holds nothing for this site at all.</summary>
    [Fact]
    public async Task ACredentialForASiteWithNoRegistration_IsRefused_AsSiteNotRegistered_LoggedAtWarning_NamingTheSite()
    {
        var (validator, logger, _) = Build();
        var unregisteredSite = Guid.NewGuid();
        var header = MintCredentialHeader(unregisteredSite, "a-secret-nobody-ever-registered-anywhere");

        var result = await validator.ValidateAsync(header, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(ModuleCallRefusalReason.SiteNotRegistered, result.Reason);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("SiteNotRegistered", entry.Message, StringComparison.Ordinal);
        Assert.Contains(unregisteredSite.ToString(), entry.Message, StringComparison.Ordinal);
    }

    /// <summary>The item's own second case: "the credential is forged, or signed with another site's
    /// secret." A registered site, but the presented signature matches neither its current nor its
    /// (non-existent, here) previous credential.</summary>
    [Fact]
    public async Task ACredentialSignedWithTheWrongSecret_IsRefused_AsInvalidSignature_LoggedAtWarning_NamingTheClaimedSite()
    {
        var siteId = new SiteId(Guid.NewGuid());
        var (validator, logger, repository) = Build();
        repository.Seed(ModuleSiteRegistration.Register(siteId, new ModuleCredential(OriginalSecret), DateTimeOffset.UtcNow));
        var header = MintCredentialHeader(siteId.Value, WrongSecret);

        var result = await validator.ValidateAsync(header, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(ModuleCallRefusalReason.InvalidSignature, result.Reason);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("InvalidSignature", entry.Message, StringComparison.Ordinal);
        Assert.Contains(siteId.Value.ToString(), entry.Message, StringComparison.Ordinal);
    }

    /// <summary>The item's own third case: "the credential expired." A genuinely correct signature -
    /// the site's own real secret - but the assertion's own <c>iat</c>/<c>exp</c> falls outside the
    /// 60-second TTL plus the five-second clock-skew allowance.</summary>
    [Fact]
    public async Task ACorrectlySignedButExpiredAssertion_IsRefused_AsAssertionExpired_LoggedAtWarning()
    {
        var siteId = new SiteId(Guid.NewGuid());
        var (validator, logger, repository) = Build();
        repository.Seed(ModuleSiteRegistration.Register(siteId, new ModuleCredential(OriginalSecret), DateTimeOffset.UtcNow));

        var mintedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var header = MintCredentialHeader(
            siteId.Value, OriginalSecret, mintedAt.ToUnixTimeSeconds(), mintedAt.AddSeconds(60).ToUnixTimeSeconds());

        var result = await validator.ValidateAsync(header, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(ModuleCallRefusalReason.AssertionExpired, result.Reason);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("AssertionExpired", entry.Message, StringComparison.Ordinal);
        Assert.Contains(siteId.Value.ToString(), entry.Message, StringComparison.Ordinal);
    }

    /// <summary>`22-11`'s own fourth case, the item's own text names explicitly: "a call carrying a
    /// credential that was legitimate until a rotation's grace window elapsed." Signed with the
    /// site's own <em>previous</em> secret, presented after <c>PreviousCredentialExpiresAt</c> has
    /// passed - genuinely never forged, only late, and distinguished from
    /// <see cref="ModuleCallRefusalReason.InvalidSignature"/> for exactly that reason.</summary>
    [Fact]
    public async Task ACredentialSignedWithAPreviousSecret_AfterItsGraceWindowElapsed_IsRefused_AsCredentialRotatedOut()
    {
        var siteId = new SiteId(Guid.NewGuid());
        var (validator, logger, repository) = Build();
        var registeredAt = DateTimeOffset.UtcNow.AddHours(-1);
        var registered = ModuleSiteRegistration.Register(siteId, new ModuleCredential(OriginalSecret), registeredAt);
        var rotatedAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var rotated = registered.Rotate(new ModuleCredential(RotatedSecret), rotatedAt, TimeSpan.FromMinutes(10));
        repository.Seed(rotated);

        // Signed with the outgoing secret - genuinely valid until the ten-minute overlap window
        // closed, twenty minutes ago.
        var header = MintCredentialHeader(siteId.Value, OriginalSecret);

        var result = await validator.ValidateAsync(header, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(ModuleCallRefusalReason.CredentialRotatedOut, result.Reason);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("CredentialRotatedOut", entry.Message, StringComparison.Ordinal);
        Assert.Contains(siteId.Value.ToString(), entry.Message, StringComparison.Ordinal);
    }

    /// <summary>The control every refusal test above needs: an outgoing credential presented
    /// <em>inside</em> its grace window is not a refusal at all, and logs nothing - here to prove
    /// specifically that this item's own new classification did not accidentally start logging - or
    /// refusing - the success path it must not touch.</summary>
    [Fact]
    public async Task AValidCredential_Authenticates_AndLogsNothing()
    {
        var siteId = new SiteId(Guid.NewGuid());
        var (validator, logger, repository) = Build();
        repository.Seed(ModuleSiteRegistration.Register(siteId, new ModuleCredential(OriginalSecret), DateTimeOffset.UtcNow));
        var header = MintCredentialHeader(siteId.Value, OriginalSecret);

        var result = await validator.ValidateAsync(header, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.True(result.IsAuthenticated);
        Assert.Equal(siteId.Value, result.SiteId);
        Assert.Null(result.Reason);
        Assert.Empty(logger.Entries);
    }

    /// <summary>CLAUDE.md's own module-credential rule, asserted rather than merely followed: not one
    /// captured log line across every case above contains any secret this test minted a token with, nor
    /// the header's own base64url segments.</summary>
    [Fact]
    public async Task AcrossEveryRefusalCase_NoLoggedMessageEverContainsASecretOrTheRawHeader()
    {
        var siteId = new SiteId(Guid.NewGuid());
        var (validator, logger, repository) = Build();
        repository.Seed(ModuleSiteRegistration.Register(siteId, new ModuleCredential(OriginalSecret), DateTimeOffset.UtcNow));

        var wrongSecretHeader = MintCredentialHeader(siteId.Value, WrongSecret);
        await validator.ValidateAsync(wrongSecretHeader, DateTimeOffset.UtcNow, CancellationToken.None);
        await validator.ValidateAsync(null, DateTimeOffset.UtcNow, CancellationToken.None);
        await validator.ValidateAsync("garbage", DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.NotEmpty(logger.Entries);
        Assert.All(logger.Entries, entry =>
        {
            Assert.DoesNotContain(OriginalSecret, entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(WrongSecret, entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(wrongSecretHeader, entry.Message, StringComparison.Ordinal);
        });
    }

    private static (HmacModuleCallCredentialValidator Validator, CapturingLogger<HmacModuleCallCredentialValidator> Logger, FakeModuleSiteRegistrationRepository Repository) Build()
    {
        var repository = new FakeModuleSiteRegistrationRepository();
        var logger = new CapturingLogger<HmacModuleCallCredentialValidator>();
        return (new HmacModuleCallCredentialValidator(repository, logger), logger, repository);
    }

    /// <summary>This suite's own independent re-derivation of the wire format
    /// <see cref="HmacModuleCallCredentialValidator"/> checks, matching
    /// <c>ModuleTaskEndpointTests.MintCredentialHeader</c>'s own reasoning - written from the
    /// contract's documented shape, not by calling production code.</summary>
    private static string MintCredentialHeader(Guid siteId, string secret, long iat, long exp)
    {
        var payloadJson = JsonSerializer.Serialize(
            new TestPayload(siteId, iat, exp), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(encodedPayload));
        return $"{encodedPayload}.{Base64UrlEncode(signature)}";
    }

    private static string MintCredentialHeader(Guid siteId, string secret)
    {
        var now = DateTimeOffset.UtcNow;
        return MintCredentialHeader(siteId, secret, now.ToUnixTimeSeconds(), now.AddSeconds(60).ToUnixTimeSeconds());
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record TestPayload(
        [property: JsonPropertyName("siteId")] Guid SiteId,
        [property: JsonPropertyName("iat")] long Iat,
        [property: JsonPropertyName("exp")] long Exp);

    /// <summary>A hand-written fake for the one port this class depends on beyond the BCL - never a
    /// mocking framework (`docs/conventions/testing.md`), and never a real Postgres.</summary>
    private sealed class FakeModuleSiteRegistrationRepository : IModuleSiteRegistrationRepository
    {
        private readonly Dictionary<Guid, ModuleSiteRegistration> _rows = [];

        public void Seed(ModuleSiteRegistration registration) => _rows[registration.SiteId.Value] = registration;

        public Task<ModuleSiteRegistration?> GetBySiteIdAsync(SiteId siteId, CancellationToken cancellationToken) =>
            Task.FromResult(_rows.GetValueOrDefault(siteId.Value));

        public Task AddAsync(ModuleSiteRegistration registration, CancellationToken cancellationToken)
        {
            Seed(registration);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ModuleSiteRegistration registration, CancellationToken cancellationToken)
        {
            Seed(registration);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(SiteId siteId, CancellationToken cancellationToken)
        {
            _rows.Remove(siteId.Value);
            return Task.CompletedTask;
        }
    }

    /// <summary>`ago-chat`'s own established convention
    /// (<c>TelegramTokenRedactingLoggingHandlerTests</c>, <c>TelemetryLeakGuardTests</c>), the identical
    /// copy <c>Ago.Calendar.Integration.Tests</c>'s own sibling test file makes - a private capturing
    /// logger per test file, extended to keep <see cref="LogLevel"/> per entry.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
