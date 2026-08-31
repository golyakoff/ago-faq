using System.Net;
using System.Net.Http.Json;
using Ago.Faq.Contracts;
using Ago.Faq.Domain;

namespace Ago.Faq.Integration.Tests;

/// <summary>
/// The console-facing knowledge-base configuration surface - proven over a real HTTP request against
/// a real host and a real Postgres. <see cref="FaqConsoleApiFactory"/> stands in for a real Keycloak
/// bearer token (that factory's own remarks explain what it does and does not fake).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class KnowledgeBaseEndpointTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Get_WithNoBearerToken_Returns401()
    {
        await using var factory = new FaqConsoleApiFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/sites/{Guid.NewGuid()}/knowledge-base");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithNoBearerToken_Returns401()
    {
        await using var factory = new FaqConsoleApiFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/sites/{Guid.NewGuid()}/knowledge-base", new PutKnowledgeBaseRequest("text"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_NeverConfigured_Returns200WithEmptyTextAndNullUpdatedAt()
    {
        await using var factory = new FaqConsoleApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(FaqConsoleApiFactory.SubjectHeader, "operator-1");

        var response = await client.GetAsync($"/api/v1/sites/{Guid.NewGuid()}/knowledge-base");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<KnowledgeBaseResponse>();
        Assert.NotNull(body);
        Assert.Equal(string.Empty, body!.Text);
        Assert.Null(body.UpdatedAt);
    }

    [Fact]
    public async Task Put_ThenGet_RoundTripsTheSavedText()
    {
        await using var factory = new FaqConsoleApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(FaqConsoleApiFactory.SubjectHeader, "operator-1");
        var siteId = Guid.NewGuid();

        var putResponse = await client.PutAsJsonAsync(
            $"/api/v1/sites/{siteId}/knowledge-base", new PutKnowledgeBaseRequest("We ship worldwide."));
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var putBody = await putResponse.Content.ReadFromJsonAsync<KnowledgeBaseResponse>();
        Assert.Equal("We ship worldwide.", putBody!.Text);
        Assert.NotNull(putBody.UpdatedAt);

        var getResponse = await client.GetAsync($"/api/v1/sites/{siteId}/knowledge-base");
        var getBody = await getResponse.Content.ReadFromJsonAsync<KnowledgeBaseResponse>();
        Assert.Equal("We ship worldwide.", getBody!.Text);
        // Within a microsecond, not exactly equal: the PUT response's UpdatedAt is the in-memory
        // IClock value (full tick precision); the GET response's own value made a real round trip
        // through Postgres' timestamptz column, which stores microsecond precision - an honest,
        // expected precision loss (date-and-time.md, ago-root), not a bug to paper over with an exact
        // equality this column can never actually satisfy.
        Assert.True((putBody.UpdatedAt!.Value - getBody.UpdatedAt!.Value).Duration() < TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Put_TextExceedingMaxLength_Returns400()
    {
        await using var factory = new FaqConsoleApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(FaqConsoleApiFactory.SubjectHeader, "operator-1");
        var tooLong = new string('a', KnowledgeBase.MaxTextLength + 1);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/sites/{Guid.NewGuid()}/knowledge-base", new PutKnowledgeBaseRequest(tooLong));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
