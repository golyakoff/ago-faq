using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ago.Faq.Contracts;
using static Ago.Faq.Integration.Tests.FaqApiFactory;

namespace Ago.Faq.Integration.Tests;

/// <summary>
/// `22-11`: the generic provisioning surface, over real HTTP, a real Postgres and the real host - the
/// identical central claim <c>Ago.Calendar.Integration.Tests.ModuleRegistrationEndpointTests</c> proves
/// for its sibling: <b>a chat-originated module call that failed before a registration existed
/// succeeds after one was provisioned through this real path, with no row inserted by hand</b> - unlike
/// <see cref="ModuleTaskEndpointTests"/>'s own <c>RegisterSiteAsync</c>, which seeds a row directly
/// through the repository because no provisioning endpoint existed yet when that suite was written.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ModuleRegistrationEndpointTests(PostgresFixture fixture)
{
    private const string ProvisioningSecretHeaderName = "X-Ago-Module-Provisioning-Secret";
    private const string CredentialHeaderName = "X-Ago-Module-Credential";

    // ------------------------------------------------------------------------------------------
    // The item's own central claim.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task AModuleTaskCall_FailsBeforeRegistration_AndSucceedsAfterItIsProvisionedThroughTheRealEndpoint()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();
        var siteId = Guid.NewGuid();
        const string credential = "a-freshly-provisioned-secret-of-sufficient-length";

        // Before: no ModuleSiteRegistration row exists for this site at all.
        var before = await StartModuleTaskAsync(client, siteId, credential);
        Assert.Equal(HttpStatusCode.Unauthorized, before.StatusCode);

        // The real path: PUT .../module-registrations/{siteId}, authenticated by the provisioning
        // secret - not a direct repository write.
        var registerResponse = await RegisterAsync(client, siteId, credential);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // After: the identical call, signed with the credential that registration just accepted,
        // now succeeds.
        var after = await StartModuleTaskAsync(client, siteId, credential);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task Register_ForASiteAlreadyRegistered_Returns409_AndDoesNotDisturbTheExistingRow()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();
        var siteId = Guid.NewGuid();
        const string original = "the-sites-original-secret-of-enough-length";
        await RegisterAsync(client, siteId, original);

        var second = await RegisterAsync(client, siteId, "a-different-secret-nobody-should-accept");
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var stillWorks = await StartModuleTaskAsync(client, siteId, original);
        Assert.Equal(HttpStatusCode.OK, stillWorks.StatusCode);
    }

    [Fact]
    public async Task Register_WithAWrongProvisioningSecret_IsRefused_AndWritesNothing()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();
        var siteId = Guid.NewGuid();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/module-registrations/{siteId}")
        {
            Content = JsonContent.Create(new { credential = "a-perfectly-valid-shaped-secret-value" }),
        };
        request.Headers.Add(ProvisioningSecretHeaderName, "not-the-configured-provisioning-secret");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var status = await GetStatusAsync(client, siteId);
        Assert.False(status.Exists);
    }

    [Fact]
    public async Task AnUnmappedSiblingRoute_Returns404()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/module-registrations-nonexistent-sibling-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------------------------------
    // Rotation.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Rotate_TheOldCredential_StillVerifiesImmediatelyAfterRotation()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();
        var siteId = Guid.NewGuid();
        const string original = "the-original-secret-before-any-rotation-x";
        const string rotated = "the-brand-new-secret-installed-by-rotation";
        await RegisterAsync(client, siteId, original);

        var rotateResponse = await RotateAsync(client, siteId, rotated);
        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);

        var withOldCredential = await StartModuleTaskAsync(client, siteId, original);
        Assert.Equal(HttpStatusCode.OK, withOldCredential.StatusCode);

        var withNewCredential = await StartModuleTaskAsync(client, siteId, rotated);
        Assert.Equal(HttpStatusCode.OK, withNewCredential.StatusCode);
    }

    /// <summary>The Done-when's own exact wording: rotating <b>one</b> site does not cost
    /// <b>another</b> site anything.</summary>
    [Fact]
    public async Task Rotate_OneSitesCredential_DoesNotAffectAnotherSitesRegistration()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        const string aOriginal = "site-as-secret-before-any-rotation-xxxxxx";
        const string aRotated = "site-as-secret-after-being-rotated-xxxxxx";
        const string bSecret = "site-bs-own-untouched-secret-xxxxxxxxxxxx";
        await RegisterAsync(client, siteA, aOriginal);
        await RegisterAsync(client, siteB, bSecret);

        var rotateResponse = await RotateAsync(client, siteA, aRotated);
        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);

        var bStillWorks = await StartModuleTaskAsync(client, siteB, bSecret);
        Assert.Equal(HttpStatusCode.OK, bStillWorks.StatusCode);
    }

    [Fact]
    public async Task Rotate_ForASiteWithNoRegistration_Returns404()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var response = await RotateAsync(client, Guid.NewGuid(), "a-secret-for-a-site-nobody-ever-registered");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------------------------------
    // Revocation.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ACallThatSucceeded_ThenARevoke_ThenTheSameCall_IsRefused()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();
        var siteId = Guid.NewGuid();
        const string credential = "a-secret-that-will-shortly-be-revoked-xxx";
        await RegisterAsync(client, siteId, credential);

        var beforeRevoke = await StartModuleTaskAsync(client, siteId, credential);
        Assert.Equal(HttpStatusCode.OK, beforeRevoke.StatusCode);

        var revokeResponse = await RevokeAsync(client, siteId);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        var afterRevoke = await StartModuleTaskAsync(client, siteId, credential);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);
    }

    [Fact]
    public async Task Revoke_ForASiteWithNoRegistration_Returns404()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var response = await RevokeAsync(client, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------------------------------
    // Detectable drift.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task GetStatus_ForASiteWithNoRegistration_ReportsNotExists()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var status = await GetStatusAsync(client, Guid.NewGuid());

        Assert.False(status.Exists);
    }

    [Fact]
    public async Task GetStatus_AfterRegisteringThenRevoking_ReportsNotExistsAgain()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();
        var siteId = Guid.NewGuid();
        await RegisterAsync(client, siteId, "a-secret-for-the-status-check-of-enough-length");
        await RevokeAsync(client, siteId);

        var status = await GetStatusAsync(client, siteId);

        Assert.False(status.Exists);
    }

    // ------------------------------------------------------------------------------------------

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, Guid siteId, string credential)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/module-registrations/{siteId}")
        {
            Content = JsonContent.Create(new { credential }),
        };
        request.Headers.Add(ProvisioningSecretHeaderName, TestProvisioningSecret);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> RotateAsync(HttpClient client, Guid siteId, string newCredential)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/module-registrations/{siteId}/rotate")
        {
            Content = JsonContent.Create(new { newCredential }),
        };
        request.Headers.Add(ProvisioningSecretHeaderName, TestProvisioningSecret);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> RevokeAsync(HttpClient client, Guid siteId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/module-registrations/{siteId}");
        request.Headers.Add(ProvisioningSecretHeaderName, TestProvisioningSecret);
        return await client.SendAsync(request);
    }

    private static async Task<StatusResponse> GetStatusAsync(HttpClient client, Guid siteId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/module-registrations/{siteId}");
        request.Headers.Add(ProvisioningSecretHeaderName, TestProvisioningSecret);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StatusResponse>())!;
    }

    private static async Task<HttpResponseMessage> StartModuleTaskAsync(HttpClient client, Guid siteId, string credential)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/module-tasks/")
        {
            Content = JsonContent.Create(new ModuleTaskStartRequest(Guid.NewGuid(), siteId, Guid.NewGuid(), "/faq")),
        };
        request.Headers.Add(CredentialHeaderName, MintCredentialHeader(siteId, credential));
        return await client.SendAsync(request);
    }

    private static string MintCredentialHeader(Guid siteId, string secret)
    {
        var now = DateTimeOffset.UtcNow;
        var payloadJson = JsonSerializer.Serialize(
            new TestPayload(siteId, now.ToUnixTimeSeconds(), now.AddSeconds(60).ToUnixTimeSeconds()),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(encodedPayload));
        return $"{encodedPayload}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record TestPayload(
        [property: JsonPropertyName("siteId")] Guid SiteId,
        [property: JsonPropertyName("iat")] long Iat,
        [property: JsonPropertyName("exp")] long Exp);

    private sealed record StatusResponse(bool Exists, DateTimeOffset? RegisteredAt, bool HasCredentialInGracePeriod);
}
