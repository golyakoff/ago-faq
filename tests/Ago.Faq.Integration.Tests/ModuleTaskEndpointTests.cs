using System.Net;
using System.Net.Http.Json;
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

        var response = await client.PostAsJsonAsync("/api/v1/module-tasks/", new ModuleTaskStartRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "/faq"));

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

        var response = await client.PostAsJsonAsync("/api/v1/module-tasks/", new ModuleTaskStartRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "/faq what is your return policy?"));

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
        var startResponse = await client.PostAsJsonAsync("/api/v1/module-tasks/", new ModuleTaskStartRequest(
            chatTaskId, Guid.NewGuid(), Guid.NewGuid(), "/faq"));
        var started = await startResponse.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();

        var replyResponse = await client.PostAsJsonAsync(
            $"/api/v1/module-tasks/{started!.ExternalTaskId}/replies",
            new ModuleTaskReplyRequest(chatTaskId, FaqStepKinds.Form, "what is your return policy?"));

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

        var response = await client.PostAsJsonAsync(
            $"/api/v1/module-tasks/{Guid.NewGuid()}/replies",
            new ModuleTaskReplyRequest(Guid.NewGuid(), FaqStepKinds.Form, "anything?"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reply_ToAnAlreadyCompletedTask_Returns409()
    {
        await using var factory = new FaqApiFactory(fixture);
        using var client = factory.CreateClient();

        var chatTaskId = Guid.NewGuid();
        var startResponse = await client.PostAsJsonAsync("/api/v1/module-tasks/", new ModuleTaskStartRequest(
            chatTaskId, Guid.NewGuid(), Guid.NewGuid(), "/faq an inline question already answers this"));
        var started = await startResponse.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();
        Assert.True(started!.Complete);

        var replyResponse = await client.PostAsJsonAsync(
            $"/api/v1/module-tasks/{started.ExternalTaskId}/replies",
            new ModuleTaskReplyRequest(chatTaskId, FaqStepKinds.Form, "a second question"));

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

        await using var factory = new FaqApiFactory(fixture, providerHost.BaseUrl);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/module-tasks/", new ModuleTaskStartRequest(
            Guid.NewGuid(), siteId, Guid.NewGuid(), "/faq what is your return policy?"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ModuleTaskStartResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Complete);
        Assert.Equal(FaqStepKinds.Answer, body.Step.Kind);
    }

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
