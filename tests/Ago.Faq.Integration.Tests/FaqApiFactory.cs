using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ago.Faq.Integration.Tests;

/// <summary>
/// <c>Ago.Faq.Api</c> in-process, pointed at this suite's own Postgres container - the same shape
/// <c>Ago.Calendar.Integration.Tests.CalendarApiFactory</c> uses.
///
/// <para><b><c>Operator:Authority</c> is a required setting and this supplies a URL that answers
/// nothing.</b> That is deliberate and costs nothing: <c>AddJwtBearer</c> fetches JWKS lazily, on the
/// first token it is asked to validate, and the module-task endpoint tests never present one (they are
/// <c>AllowAnonymous</c>); the knowledge-base endpoint tests use <see cref="FaqConsoleApiFactory"/>'s
/// own header-based stand-in instead of a real Keycloak token. What the setting being required proves,
/// by being required at all, is that this host has no fallback authentication and no dev stub.</para>
///
/// <para><paramref name="faqAnswerBaseUrl"/> lets a test point the OpenAI-compatible client at a fake
/// provider host (<see cref="ModuleTaskEndpointTests"/>'s own answered-question test) - null leaves the
/// feature unconfigured, the ordinary case for every other test in this suite.</para>
///
/// <para><b>`22-04`: no more shared-secret setting.</b> The deployment-wide
/// <c>ChatModule:SharedSecret</c> this factory used to bind is gone along with
/// <c>ModuleCallCredentialOptions</c> - a credential is now checked against whichever
/// <c>Ago.Faq.Domain.ModuleSiteRegistration</c> row its own claimed site id names, seeded directly
/// through <c>IModuleSiteRegistrationRepository</c> by whichever test needs one (see
/// <see cref="ModuleTaskEndpointTests"/>'s own seeding helper).</para>
/// </summary>
public class FaqApiFactory(PostgresFixture fixture, string? faqAnswerBaseUrl = null)
    : WebApplicationFactory<Program>
{
    public const string UnreachableAuthority = "https://keycloak.invalid/realms/ago";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:Faq", fixture.ConnectionString);
        builder.UseSetting("Operator:Authority", UnreachableAuthority);

        if (faqAnswerBaseUrl is not null)
        {
            builder.UseSetting("FaqAnswer:OpenAiCompatible:ApiKey", "test-key");
            builder.UseSetting("FaqAnswer:OpenAiCompatible:BaseUrl", faqAnswerBaseUrl);
            builder.UseSetting("FaqAnswer:OpenAiCompatible:Model", "test-model");
        }
    }
}

/// <summary>
/// The same host with a stand-in for Keycloak: a scheme that turns an <c>X-Test-Subject</c> header
/// into an authenticated principal, and nothing else - the same "fake exactly one thing, run every
/// other step for real" shape <c>Ago.Calendar.Integration.Tests.ConsoleApiFactory</c> uses. Unlike
/// that factory, this module resolves no local operator/tenant row from the subject - there is none to
/// resolve (<c>KnowledgeBaseEndpoints</c>'s own remarks on the named authorization gap) - so the fake
/// only has to prove "a request without a valid bearer token is refused" and "a request with one is
/// let through", not exercise a claims-transformation pipeline this module does not have.
/// </summary>
public sealed class FaqConsoleApiFactory(PostgresFixture fixture) : FaqApiFactory(fixture)
{
    public const string SubjectHeader = "X-Test-Subject";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, HeaderSubjectAuthenticationHandler>(
                    HeaderSubjectAuthenticationHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = HeaderSubjectAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = HeaderSubjectAuthenticationHandler.SchemeName;
            });
        });
    }
}

internal sealed class HeaderSubjectAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestSubject";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var subject = Request.Headers[FaqConsoleApiFactory.SubjectHeader].ToString();
        if (string.IsNullOrWhiteSpace(subject))
        {
            // NoResult, not Fail: "nobody presented anything" is an anonymous request, and
            // RequireAuthorization() is what turns that into a 401.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim("sub", subject)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
