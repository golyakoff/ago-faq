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
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ModuleTaskEndpointTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    /// <summary>This suite's own shared secret, matching what <see cref="FaqApiFactory"/> configures
    /// as <c>ChatModule:SharedSecret</c> for every factory this file builds.</summary>
    private const string TestSharedSecret = "integration-test-shared-secret-of-sufficient-length";

    [Fact]
    public async Task Start_BareTrigger_ReturnsFormStep_NotComplete()
    {
        await using var factory = new FaqApiFactory(fixture, moduleCredentialSharedSecret: TestSharedSecret);
        using var client = factory.CreateClient();

        var response = await StartAsync(client, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "/faq");

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
        await using var factory = new FaqApiFactory(fixture, moduleCredentialSharedSecret: TestSharedSecret);
        using var client = factory.CreateClient();

        var response = await StartAsync(
            client, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "/faq what is your return policy?");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Complete);
        Assert.Equal(FaqStepKinds.Escalate, body.Step.Kind);
    }

    [Fact]
    public async Task Reply_ToBareTriggerTask_WithNoKnowledgeBase_CompletesWithEscalate()
    {
        await using var factory = new FaqApiFactory(fixture, moduleCredentialSharedSecret: TestSharedSecret);
        using var client = factory.CreateClient();

        var chatTaskId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var startResponse = await StartAsync(client, chatTaskId, siteId, Guid.NewGuid(), "/faq");
        var started = await startResponse.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();

        var replyResponse = await ReplyAsync(
            client, started!.ExternalTaskId, siteId, chatTaskId, FaqStepKinds.Form, "what is your return policy?");

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
        await using var factory = new FaqApiFactory(fixture, moduleCredentialSharedSecret: TestSharedSecret);
        using var client = factory.CreateClient();

        var response = await ReplyAsync(
            client, Guid.NewGuid().ToString(), Guid.NewGuid(), Guid.NewGuid(), FaqStepKinds.Form, "anything?");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reply_ToAnAlreadyCompletedTask_Returns409()
    {
        await using var factory = new FaqApiFactory(fixture, moduleCredentialSharedSecret: TestSharedSecret);
        using var client = factory.CreateClient();

        var chatTaskId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var startResponse = await StartAsync(
            client, chatTaskId, siteId, Guid.NewGuid(), "/faq an inline question already answers this");
        var started = await startResponse.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();
        Assert.True(started!.Complete);

        var replyResponse = await ReplyAsync(
            client, started.ExternalTaskId, siteId, chatTaskId, FaqStepKinds.Form, "a second question");

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

        await using var providerHost = await BuildFakeProviderHostAsync(app =>
            app.MapPost("chat/completions", () => Results.Json(new
            {
                choices = new[]
                {
                    new { message = new { role = "assistant", content = "You can return items within 30 days." } },
                },
            })));

        await using var factory = new FaqApiFactory(fixture, providerHost.BaseUrl, TestSharedSecret);
        using var client = factory.CreateClient();

        var response = await StartAsync(
            client, Guid.NewGuid(), siteId, Guid.NewGuid(), "/faq what is your return policy?");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Complete);
        Assert.Equal(FaqStepKinds.Answer, body.Step.Kind);
    }

    // ------------------------------------------------------------------------------------------
    // `22-02`'s own three-directional security claim, over the real host and a real signature
    // check. AnUnmappedSiblingRoute_Returns404 is this suite's own distinguishable-401-vs-404
    // control (`20-24`'s lesson) - Reply_ToUnknownExternalTaskId_Returns404 above already proves
    // the "known route, unknown resource" half.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task StartWithNoCredentialHeader_IsRefused()
    {
        await using var factory = new FaqApiFactory(fixture, moduleCredentialSharedSecret: TestSharedSecret);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/module-tasks/", new ModuleTaskStartRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "/faq"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StartWithAWrongCredential_IsRefused()
    {
        await using var factory = new FaqApiFactory(fixture, moduleCredentialSharedSecret: TestSharedSecret);
        using var client = factory.CreateClient();

        var siteId = Guid.NewGuid();
        var response = await StartAsync(
            client, Guid.NewGuid(), siteId, Guid.NewGuid(), "/faq", MintCredentialHeader(siteId, "a-completely-different-secret-value"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>The sharpest claim: a credential genuinely valid for one site cannot name another in
    /// the Start body.</summary>
    [Fact]
    public async Task StartWithACredentialForAnotherSite_IsRefused()
    {
        await using var factory = new FaqApiFactory(fixture, moduleCredentialSharedSecret: TestSharedSecret);
        using var client = factory.CreateClient();

        var credentialedSiteId = Guid.NewGuid();
        var differentBodySiteId = Guid.NewGuid();
        var response = await StartAsync(
            client, Guid.NewGuid(), differentBodySiteId, Guid.NewGuid(), "/faq",
            MintCredentialHeader(credentialedSiteId, TestSharedSecret));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>The property this module proves that Calendar's identical route cannot yet
    /// (<c>ModuleTaskEndpoints</c>'s own remarks): this module already stores a site id per task, so a
    /// credential valid for site A is refused - as if the task did not exist - against a task that
    /// belongs to site B, not merely authenticated and trusted.</summary>
    [Fact]
    public async Task ReplyWithACredentialForAnotherSite_IsRefused_AsIfTheTaskDidNotExist()
    {
        await using var factory = new FaqApiFactory(fixture, moduleCredentialSharedSecret: TestSharedSecret);
        using var client = factory.CreateClient();

        var chatTaskId = Guid.NewGuid();
        var siteA = Guid.NewGuid();
        var startResponse = await StartAsync(client, chatTaskId, siteA, Guid.NewGuid(), "/faq");
        var started = await startResponse.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();

        var siteB = Guid.NewGuid();
        var replyResponse = await ReplyAsync(
            client, started!.ExternalTaskId, siteB, chatTaskId, FaqStepKinds.Form, "what is your return policy?");

        Assert.Equal(HttpStatusCode.NotFound, replyResponse.StatusCode);
    }

    [Fact]
    public async Task AnUnmappedSiblingRoute_Returns404()
    {
        await using var factory = new FaqApiFactory(fixture, moduleCredentialSharedSecret: TestSharedSecret);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/module-tasks-nonexistent-sibling-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> StartAsync(
        HttpClient client, Guid chatTaskId, Guid siteId, Guid conversationId, string triggerText, string? credentialHeader = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/module-tasks/")
        {
            Content = JsonContent.Create(new ModuleTaskStartRequest(chatTaskId, siteId, conversationId, triggerText)),
        };
        request.Headers.Add("X-Ago-Module-Credential", credentialHeader ?? MintCredentialHeader(siteId, TestSharedSecret));
        // Awaited here, not returned as a bare Task - `request` is disposed by this method's own
        // `using` the moment it returns, and disposing it before SendAsync has finished reading its
        // content throws ObjectDisposedException from inside the TestServer pipeline (found by this
        // item's own fails-before run, not by inspection - see ago-calendar's identical fix).
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> ReplyAsync(
        HttpClient client, string externalTaskId, Guid siteId, Guid chatTaskId, string kind, string value,
        string? credentialHeader = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/module-tasks/{externalTaskId}/replies")
        {
            Content = JsonContent.Create(new ModuleTaskReplyRequest(chatTaskId, kind, value)),
        };
        request.Headers.Add("X-Ago-Module-Credential", credentialHeader ?? MintCredentialHeader(siteId, TestSharedSecret));
        return await client.SendAsync(request);
    }

    /// <summary>`22-02`: written from <c>HmacModuleCallCredentialValidator</c>'s own documented
    /// contract, not by calling production code - see this class's own remarks.</summary>
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
