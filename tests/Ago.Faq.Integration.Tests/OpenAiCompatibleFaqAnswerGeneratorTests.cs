using Ago.Faq.Application.Abstractions;
using Ago.Faq.Infrastructure.OpenAiCompatible;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ago.Faq.Integration.Tests;

/// <summary>
/// <see cref="OpenAiCompatibleFaqAnswerGenerator"/>'s own real HTTP boundary, proven against a fake
/// OpenAI-compatible provider standing in for a real one - the same technique
/// `ago-chat`'s own <c>YandexGptReplyDraftClientTests</c> establishes (a real, in-process,
/// ephemeral-port Kestrel host, not a mocked <see cref="HttpMessageHandler"/>).
///
/// <para><b>Not confirmed against a real provider</b> - no real OpenAI-compatible API key exists in
/// any environment this project runs in yet. This proves the request shape this codebase sends and
/// that every documented response/error shape this class handles is actually handled the way the code
/// claims - not that any real provider's own service behaves as documented.</para>
/// </summary>
public sealed class OpenAiCompatibleFaqAnswerGeneratorTests
{
    [Fact]
    public async Task SendsTheKnowledgeBaseAndQuestion_AndParsesATrimmedAnswer()
    {
        string? capturedBody = null;

        await using var host = await BuildFakeProviderHostAsync(app =>
            app.MapPost("chat/completions", async (HttpContext context) =>
            {
                using var reader = new StreamReader(context.Request.Body);
                capturedBody = await reader.ReadToEndAsync();
                return Results.Json(new
                {
                    choices = new[]
                    {
                        new { message = new { role = "assistant", content = "  Yes, we ship worldwide.  " } },
                    },
                });
            }));

        var generator = BuildGenerator(host.BaseUrl);

        var result = await generator.AnswerAsync(
            new FaqAnswerRequest("We ship worldwide, 5-7 business days.", "do you ship internationally?"),
            CancellationToken.None);

        var answered = Assert.IsType<FaqAnswerResult.Answered>(result);
        Assert.Equal("Yes, we ship worldwide.", answered.AnswerText);

        Assert.Contains("We ship worldwide, 5-7 business days.", capturedBody);
        Assert.Contains("do you ship internationally?", capturedBody);
        Assert.Contains("\"role\":\"system\"", capturedBody);
        Assert.Contains("\"role\":\"user\"", capturedBody);
    }

    [Fact]
    public async Task NotFoundToken_ReturnsNotGrounded()
    {
        await using var host = await BuildFakeProviderHostAsync(app =>
            app.MapPost("chat/completions", () => Results.Json(new
            {
                choices = new[] { new { message = new { role = "assistant", content = "NOT_FOUND" } } },
            })));

        var generator = BuildGenerator(host.BaseUrl);

        var result = await generator.AnswerAsync(
            new FaqAnswerRequest("We ship worldwide.", "what is the meaning of life?"), CancellationToken.None);

        Assert.IsType<FaqAnswerResult.NotGrounded>(result);
    }

    [Fact]
    public async Task NotFoundToken_IsCaseInsensitive_AndToleratesSurroundingWhitespace()
    {
        await using var host = await BuildFakeProviderHostAsync(app =>
            app.MapPost("chat/completions", () => Results.Json(new
            {
                choices = new[] { new { message = new { role = "assistant", content = "  not_found  " } } },
            })));

        var generator = BuildGenerator(host.BaseUrl);

        var result = await generator.AnswerAsync(
            new FaqAnswerRequest("We ship worldwide.", "anything?"), CancellationToken.None);

        Assert.IsType<FaqAnswerResult.NotGrounded>(result);
    }

    [Fact]
    public async Task EmptyContent_ReturnsNotGrounded()
    {
        await using var host = await BuildFakeProviderHostAsync(app =>
            app.MapPost("chat/completions", () => Results.Json(new
            {
                choices = new[] { new { message = new { role = "assistant", content = "" } } },
            })));

        var generator = BuildGenerator(host.BaseUrl);

        var result = await generator.AnswerAsync(
            new FaqAnswerRequest("We ship worldwide.", "anything?"), CancellationToken.None);

        Assert.IsType<FaqAnswerResult.NotGrounded>(result);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status429TooManyRequests)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    public async Task NonSuccessStatus_ReturnsUnavailable_NeverThrows(int statusCode)
    {
        await using var host = await BuildFakeProviderHostAsync(app =>
            app.MapPost("chat/completions", () => Results.StatusCode(statusCode)));

        var generator = BuildGenerator(host.BaseUrl);

        var result = await generator.AnswerAsync(
            new FaqAnswerRequest("We ship worldwide.", "anything?"), CancellationToken.None);

        Assert.IsType<FaqAnswerResult.Unavailable>(result);
    }

    [Fact]
    public async Task UnreachableProvider_ReturnsUnavailable_NeverThrows()
    {
        await using var host = await BuildFakeProviderHostAsync(app =>
            app.MapPost("chat/completions", () => Results.Ok()));
        var generator = BuildGenerator(host.BaseUrl);
        await host.App.StopAsync();

        var result = await generator.AnswerAsync(
            new FaqAnswerRequest("We ship worldwide.", "anything?"), CancellationToken.None);

        Assert.IsType<FaqAnswerResult.Unavailable>(result);
    }

    [Fact]
    public async Task UnparseableJsonBody_ReturnsUnavailable_NeverThrows()
    {
        await using var host = await BuildFakeProviderHostAsync(app =>
            app.MapPost("chat/completions", () => Results.Text("this is not json", "application/json")));

        var generator = BuildGenerator(host.BaseUrl);

        var result = await generator.AnswerAsync(
            new FaqAnswerRequest("We ship worldwide.", "anything?"), CancellationToken.None);

        Assert.IsType<FaqAnswerResult.Unavailable>(result);
    }

    private static OpenAiCompatibleFaqAnswerGenerator BuildGenerator(string baseUrl)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var options = Options.Create(new OpenAiCompatibleFaqAnswerOptions
        {
            ApiKey = "test-key",
            BaseUrl = baseUrl,
            Model = "test-model",
            MaxTokens = 200,
        });
        return new OpenAiCompatibleFaqAnswerGenerator(httpClient, options);
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
