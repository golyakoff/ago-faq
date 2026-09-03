using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ago.Faq.Contracts;
using Ago.Faq.Domain;
using Ago.Faq.Infrastructure.Postgres;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ago.Faq.Integration.Tests;

/// <summary>
/// The wire contract Ago.Chat.* drives this module through, proven end to end over a real HTTP
/// request against a real, in-process host and a real Postgres - the same bar
/// <c>Ago.Calendar.Integration.Tests.BookingEndpointTests</c> sets for Calendar's equivalent surface.
///
/// <para><b>`22-02`: every call in this suite now carries a real, signed credential header</b> -
/// <see cref="MintCredentialHeader"/> is this suite's own independent re-derivation of the wire format
/// <c>HmacModuleCallCredentialValidator</c> checks (written from that class's own documented contract,
/// not by calling production code), matching <c>Ago.Calendar.Integration.Tests.ChatModuleTaskEndpointTests</c>'s
/// identical approach for the sibling product.</para>
///
/// <para><b>`22-04`: no more one shared secret for the whole suite.</b> Each test that needs an
/// authenticated call registers the exact site(s) it uses through
/// <see cref="RegisterSiteAsync"/> - a direct write through <c>IModuleSiteRegistrationRepository</c>,
/// the same "seed through the store, not through a console endpoint that does not exist yet" pattern
/// <c>Start_InlineQuestion_WithConfiguredKnowledgeBaseAndProvider_ReturnsGroundedAnswer</c> already
/// uses for <c>KnowledgeBaseRepository</c>. A site with no such row is exactly "the module is not
/// enabled for this site" - <see cref="StartForAnUnregisteredSite_IsRefused"/> proves it is refused,
/// not answered by falling back to anybody else's secret.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ModuleTaskEndpointTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Start_BareTrigger_ReturnsFormStep_NotComplete()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var siteId = Guid.NewGuid();
        const string secret = "site-secret-of-sufficient-length-aaaaaa";
        await RegisterSiteAsync(siteId, secret);

        var response = await StartAsync(
            client, Guid.NewGuid(), siteId, Guid.NewGuid(), "/faq", MintCredentialHeader(siteId, secret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();
        Assert.NotNull(body);
        Assert.False(body!.Complete);
        Assert.Equal(FaqStepKinds.Form, body.Step.Kind);
        Assert.False(string.IsNullOrWhiteSpace(body.ExternalTaskId));
    }

    [Fact]
    public async Task Start_InlineQuestion_NoKnowledgeBaseConfigured_ReturnsEscalate_Complete()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var siteId = Guid.NewGuid();
        const string secret = "site-secret-of-sufficient-length-bbbbbb";
        await RegisterSiteAsync(siteId, secret);

        var response = await StartAsync(
            client, Guid.NewGuid(), siteId, Guid.NewGuid(), "/faq what is your return policy?",
            MintCredentialHeader(siteId, secret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Complete);
        Assert.Equal(FaqStepKinds.Escalate, body.Step.Kind);
    }

    [Fact]
    public async Task Reply_ToBareTriggerTask_WithNoKnowledgeBase_CompletesWithEscalate()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var chatTaskId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        const string secret = "site-secret-of-sufficient-length-cccccc";
        await RegisterSiteAsync(siteId, secret);

        var startResponse = await StartAsync(
            client, chatTaskId, siteId, Guid.NewGuid(), "/faq", MintCredentialHeader(siteId, secret));
        var started = await startResponse.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();

        var replyResponse = await ReplyAsync(
            client, started!.ExternalTaskId, siteId, chatTaskId, FaqStepKinds.Form, "what is your return policy?",
            secret);

        Assert.Equal(HttpStatusCode.OK, replyResponse.StatusCode);
        var replied = await replyResponse.Content.ReadFromJsonAsync<ModuleTaskReplyResponse>();
        Assert.NotNull(replied);
        Assert.True(replied!.Complete);
        Assert.NotNull(replied.Step);
        Assert.Equal(FaqStepKinds.Escalate, replied.Step!.Kind);
    }

    [Fact]
    public async Task Reply_ToUnknownExternalTaskId_Returns404()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var siteId = Guid.NewGuid();
        await RegisterSiteAsync(siteId, "site-secret-of-sufficient-length-dddddd");

        var response = await ReplyAsync(
            client, Guid.NewGuid().ToString(), siteId, Guid.NewGuid(), FaqStepKinds.Form, "anything?",
            "site-secret-of-sufficient-length-dddddd");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reply_ToAnAlreadyCompletedTask_Returns409()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var chatTaskId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        const string secret = "site-secret-of-sufficient-length-eeeeee";
        await RegisterSiteAsync(siteId, secret);

        var startResponse = await StartAsync(
            client, chatTaskId, siteId, Guid.NewGuid(), "/faq an inline question already answers this",
            MintCredentialHeader(siteId, secret));
        var started = await startResponse.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();
        Assert.True(started!.Complete);

        var replyResponse = await ReplyAsync(
            client, started.ExternalTaskId, siteId, chatTaskId, FaqStepKinds.Form, "a second question", secret);

        Assert.Equal(HttpStatusCode.Conflict, replyResponse.StatusCode);
    }

    [Fact]
    public async Task Start_InlineQuestion_WithConfiguredKnowledgeBaseAndProvider_ReturnsGroundedAnswer()
    {
        // Seed a knowledge base directly through the store - the console PUT endpoint is proven
        // separately in KnowledgeBaseEndpointTests; this test is about the module-task path only.
        var siteId = Guid.NewGuid();
        await using (var db = fixture.CreateDbContext())
        {
            await new KnowledgeBaseRepository(db).UpsertAsync(
                KnowledgeBase.Create(new SiteId(siteId), "We accept returns within 30 days of delivery.", Now),
                CancellationToken.None);
        }

        const string secret = "site-secret-of-sufficient-length-ffffff";
        await RegisterSiteAsync(siteId, secret);

        await using var providerHost = await BuildFakeProviderHostAsync(app =>
            app.MapPost("chat/completions", () => Results.Json(new
            {
                choices = new[]
                {
                    new { message = new { role = "assistant", content = "You can return items within 30 days." } },
                },
            })));

        await using var factory = new FaqApiFactory(fixture, providerHost.BaseUrl);
        using var client = factory.CreateClient();

        var response = await StartAsync(
            client, Guid.NewGuid(), siteId, Guid.NewGuid(), "/faq what is your return policy?",
            MintCredentialHeader(siteId, secret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Complete);
        Assert.Equal(FaqStepKinds.Answer, body.Step.Kind);
    }

    // ------------------------------------------------------------------------------------------
    // `22-04`'s own claim: resolution is per site, proven with two sites rather than reasoned
    // about with one, and each site's secret is independently generated rather than one value
    // this whole deployment shares (adr/0094's own named limit, closed here).
    // ------------------------------------------------------------------------------------------

    /// <summary>The Done-when's own sharpest requirement: two sites, each with the module enabled
    /// under its own independently generated secret, resolve completely independently of one another
    /// in the same run - not merely "the mechanism works once".</summary>
    [Fact]
    public async Task TwoSites_EachWithItsOwnRegistration_BothStartIndependently()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        const string secretA = "independent-secret-for-site-a-aaaaaaaaaa";
        const string secretB = "a-completely-different-secret-for-site-b";
        await RegisterSiteAsync(siteA, secretA);
        await RegisterSiteAsync(siteB, secretB);

        var responseA = await StartAsync(client, Guid.NewGuid(), siteA, Guid.NewGuid(), "/faq", MintCredentialHeader(siteA, secretA));
        var responseB = await StartAsync(client, Guid.NewGuid(), siteB, Guid.NewGuid(), "/faq", MintCredentialHeader(siteB, secretB));

        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
    }

    /// <summary>A site nobody registered has no secret to be checked against - refused exactly like
    /// any other unauthenticated call, never answered by falling back to any other site's tenant or
    /// secret.</summary>
    [Fact]
    public async Task StartForAnUnregisteredSite_IsRefused()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var unregisteredSite = Guid.NewGuid();
        var response = await StartAsync(
            client, Guid.NewGuid(), unregisteredSite, Guid.NewGuid(), "/faq",
            MintCredentialHeader(unregisteredSite, "a-secret-nobody-ever-registered-anywhere"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>`22-04`'s own regression guard for adr/0094's named limit: under the old
    /// deployment-wide secret this exact call would have succeeded, because one secret verified every
    /// site. Site A's own secret is real and registered; the token merely claims to be site B's -
    /// which has its own, different, registered secret - and the signature can only ever verify
    /// against the secret the claimed site actually owns.</summary>
    [Fact]
    public async Task CredentialSignedWithSiteAsOwnSecret_ButClaimingSiteB_IsRefused()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        const string secretA = "site-a-owns-this-secret-and-nobody-else-does";
        const string secretB = "site-b-owns-a-completely-different-one-here";
        await RegisterSiteAsync(siteA, secretA);
        await RegisterSiteAsync(siteB, secretB);

        // Signed with A's own secret, but the payload claims to be B.
        var forgedForB = MintCredentialHeader(siteB, secretA);

        var response = await StartAsync(client, Guid.NewGuid(), siteB, Guid.NewGuid(), "/faq", forgedForB);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StartWithNoCredentialHeader_IsRefused()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/module-tasks/", new ModuleTaskStartRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "/faq"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StartWithAWrongCredential_IsRefused()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var siteId = Guid.NewGuid();
        await RegisterSiteAsync(siteId, "the-real-registered-secret-for-this-site");

        var response = await StartAsync(
            client, Guid.NewGuid(), siteId, Guid.NewGuid(), "/faq",
            MintCredentialHeader(siteId, "a-completely-different-secret-value-here"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>The sharpest claim: a credential genuinely valid for one site cannot name another in
    /// the Start body.</summary>
    [Fact]
    public async Task StartWithACredentialForAnotherSite_IsRefused()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var credentialedSiteId = Guid.NewGuid();
        var differentBodySiteId = Guid.NewGuid();
        const string secret = "this-sites-own-registered-secret-value-x";
        await RegisterSiteAsync(credentialedSiteId, secret);

        var response = await StartAsync(
            client, Guid.NewGuid(), differentBodySiteId, Guid.NewGuid(), "/faq",
            MintCredentialHeader(credentialedSiteId, secret));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>The property this module proves that Calendar's identical route cannot yet
    /// (<c>ModuleTaskEndpoints</c>'s own remarks): this module already stores a site id per task, so a
    /// credential valid for site A is refused - as if the task did not exist - against a task that
    /// belongs to site B, not merely authenticated and trusted.</summary>
    [Fact]
    public async Task ReplyWithACredentialForAnotherSite_IsRefused_AsIfTheTaskDidNotExist()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var chatTaskId = Guid.NewGuid();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        const string secretA = "site-a-secret-for-cross-task-reply-test-x";
        const string secretB = "site-b-secret-for-cross-task-reply-test-y";
        await RegisterSiteAsync(siteA, secretA);
        await RegisterSiteAsync(siteB, secretB);

        var startResponse = await StartAsync(client, chatTaskId, siteA, Guid.NewGuid(), "/faq", MintCredentialHeader(siteA, secretA));
        var started = await startResponse.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();

        var replyResponse = await ReplyAsync(
            client, started!.ExternalTaskId, siteB, chatTaskId, FaqStepKinds.Form, "what is your return policy?", secretB);

        Assert.Equal(HttpStatusCode.NotFound, replyResponse.StatusCode);
    }

    [Fact]
    public async Task AnUnmappedSiblingRoute_Returns404()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/module-tasks-nonexistent-sibling-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>`22-04`: seeds one <c>ModuleSiteRegistration</c> row directly through the write-side
    /// repository - the registry's own consuming half, proven separately by
    /// <c>Ago.Faq.Domain.Tests</c>; no console or provisioning endpoint exists yet to do this over
    /// HTTP (out of this item's own scope - see this item's report).</summary>
    private async Task RegisterSiteAsync(Guid siteId, string secret)
    {
        await using var db = fixture.CreateDbContext();
        await new ModuleSiteRegistrationRepository(db).AddAsync(
            ModuleSiteRegistration.Register(new SiteId(siteId), new ModuleCredential(secret), Now),
            CancellationToken.None);
    }

    private static async Task<HttpResponseMessage> StartAsync(
        HttpClient client, Guid chatTaskId, Guid siteId, Guid conversationId, string triggerText, string? credentialHeader = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/module-tasks/")
        {
            Content = JsonContent.Create(new ModuleTaskStartRequest(chatTaskId, siteId, conversationId, triggerText)),
        };
        if (credentialHeader is not null)
        {
            request.Headers.Add("X-Ago-Module-Credential", credentialHeader);
        }

        // Awaited here, not returned as a bare Task - `request` is disposed by this method's own
        // `using` the moment it returns, and disposing it before SendAsync has finished reading its
        // content throws ObjectDisposedException from inside the TestServer pipeline (found by this
        // item's own fails-before run, not by inspection - see ago-calendar's identical fix).
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> ReplyAsync(
        HttpClient client, string externalTaskId, Guid siteId, Guid chatTaskId, string kind, string value,
        string secret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/module-tasks/{externalTaskId}/replies")
        {
            Content = JsonContent.Create(new ModuleTaskReplyRequest(chatTaskId, kind, value)),
        };
        request.Headers.Add("X-Ago-Module-Credential", MintCredentialHeader(siteId, secret));
        return await client.SendAsync(request);
    }

    /// <summary>`22-02`/`22-04`: written from <c>HmacModuleCallCredentialValidator</c>'s own
    /// documented contract, not by calling production code - see this class's own remarks. The secret
    /// is now a per-call parameter rather than one constant this whole file shared, matching the
    /// per-site registration this suite seeds through <see cref="RegisterSiteAsync"/>.</summary>
    private static string MintCredentialHeader(Guid siteId, string secret, TimeSpan? expiresIn = null)
    {
        var now = DateTimeOffset.UtcNow;
        var exp = now.Add(expiresIn ?? TimeSpan.FromSeconds(60));
        var payloadJson = JsonSerializer.Serialize(
            new TestPayload(siteId, now.ToUnixTimeSeconds(), exp.ToUnixTimeSeconds()),
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

    private sealed record TestHost(WebApplication App, string BaseUrl) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await App.DisposeAsync();
    }

    private static async Task<TestHost> BuildFakeProviderHostAsync(Action<WebApplication> configureRoutes)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        configureRoutes(app);

        await app.StartAsync();
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
        var baseUrl = addresses.First() + "/";

        return new TestHost(app, baseUrl);
    }
}
