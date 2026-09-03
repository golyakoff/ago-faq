using System.Net.Http.Headers;
using Ago.Faq.Application.Abstractions;
using Ago.Faq.Application.UseCases.FaqModuleTask;
using Ago.Faq.Application.UseCases.KnowledgeBase;
using Ago.Faq.Infrastructure.OpenAiCompatible;
using Ago.Faq.Infrastructure.Postgres;
using Ago.Platform.Hosting;
using Ago.Platform.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ago.Faq.Module;

/// <summary>
/// The one <see cref="IProductModule"/> <c>Ago.Faq.Api</c> loads - the second-generation shape of the
/// platform's hosting seam, the same role `Ago.Calendar.Module.CalendarModule` plays for that product.
/// This module has exactly one host (`19-03`'s own "Decided" section, ago-root: no <c>Worker</c>, no
/// <c>Webhooks</c> - a single failure profile, nothing async to run), so every registration below is
/// used by that one host; there is no second host to keep this composition identical across.
/// </summary>
public sealed class FaqModule : IProductModule
{
    public string Name => "Ago.Faq";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString =
            configuration.GetConnectionString("Faq")
            ?? Environment.GetEnvironmentVariable("AGO_FAQ_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings:Faq or AGO_FAQ_CONNECTION_STRING - e.g. the docker-compose " +
                "Postgres from local-dev.md (ago-root).");

        services.AddFaqPostgresPersistence(connectionString);

        services.AddScoped<AnswerFaqQuestionHandler>();
        services.AddScoped<StartFaqModuleTaskHandler>();
        services.AddScoped<ReplyToFaqModuleTaskHandler>();
        services.AddScoped<GetKnowledgeBaseHandler>();
        services.AddScoped<PutKnowledgeBaseHandler>();

        // `22-04`: no more deployment-wide secret to bind at startup - a call is now authenticated
        // against the per-site Ago.Faq.Domain.ModuleSiteRegistration row its own claimed site id
        // names, read fresh on every call through the same repository AddFaqPostgresPersistence just
        // registered. Scoped, not singleton, for the identical reason every other adapter holding a
        // DbContext-backed dependency here is: it is no longer a stateless class over static
        // configuration.
        services.AddScoped<IModuleCallCredentialValidator, HmacModuleCallCredentialValidator>();

        // `22-11`: provisioning - the write that had nothing outside a test calling it. A different
        // secret and a different mechanism from the credential validator just above - see
        // IModuleProvisioningAuthenticator's own remarks.
        services.AddOptions<ModuleProvisioningOptions>()
            .Bind(configuration.GetSection(ModuleProvisioningOptions.SectionName))
            .ValidateOnStart();
        services.AddScoped<IModuleProvisioningAuthenticator, SharedSecretModuleProvisioningAuthenticator>();

        services.AddScoped<Application.UseCases.ModuleRegistration.RegisterModuleSiteHandler>();
        services.AddScoped<Application.UseCases.ModuleRegistration.RotateModuleSiteCredentialHandler>();
        services.AddScoped<Application.UseCases.ModuleRegistration.RevokeModuleSiteRegistrationHandler>();
        services.AddScoped<Application.UseCases.ModuleRegistration.GetModuleSiteRegistrationStatusHandler>();

        // `FaqAnswer:OpenAiCompatible:*` - bound, not hard-validated, at startup. The same lesson
        // ago-chat already learned once (commit d0b2ba6, "YandexGPT degrades instead of crash-looping
        // the host"): no environment has real credentials for this yet, and this optional feature must
        // not be able to take the entire host down at startup for every other route it serves.
        services
            .AddOptions<OpenAiCompatibleFaqAnswerOptions>()
            .Bind(configuration.GetSection(OpenAiCompatibleFaqAnswerOptions.SectionName));
        var faqAnswerOptions =
            configuration.GetSection(OpenAiCompatibleFaqAnswerOptions.SectionName).Get<OpenAiCompatibleFaqAnswerOptions>()
            ?? new OpenAiCompatibleFaqAnswerOptions();

        if (!string.IsNullOrWhiteSpace(faqAnswerOptions.ApiKey) && !string.IsNullOrWhiteSpace(faqAnswerOptions.BaseUrl))
        {
            services.AddHttpClient<OpenAiCompatibleFaqAnswerGenerator>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<OpenAiCompatibleFaqAnswerOptions>>().Value;
                var baseUrl = opts.BaseUrl.EndsWith('/') ? opts.BaseUrl : opts.BaseUrl + "/";
                client.BaseAddress = new Uri(baseUrl);
                // The OpenAI-compatible convention: a bare bearer token, set once here at the
                // composition root - the same "the module builds the HttpClient, the client class
                // stays thin" split ago-chat's own YandexGptReplyDraftClient/ChatModule wiring makes
                // for its own static-key header.
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiKey);
            });

            services.AddResiliencePipelineOptions(
                OpenAiCompatibleFaqAnswerResiliencePipeline.PipelineName, configuration,
                ConfigureFaqAnswerResilienceDefaults);
            services.AddSingleton(sp => new OpenAiCompatibleFaqAnswerResiliencePipeline(
                sp.GetRequiredService<IOptionsMonitor<ResiliencePipelineOptions>>()
                    .Get(OpenAiCompatibleFaqAnswerResiliencePipeline.PipelineName)));
            services.AddScoped<IFaqAnswerGenerator>(sp => new ResilientFaqAnswerGenerator(
                sp.GetRequiredService<OpenAiCompatibleFaqAnswerGenerator>(),
                sp.GetRequiredService<OpenAiCompatibleFaqAnswerResiliencePipeline>(),
                sp.GetRequiredService<ILogger<ResilientFaqAnswerGenerator>>()));
        }
        else
        {
            services.AddScoped<IFaqAnswerGenerator, UnconfiguredFaqAnswerGenerator>();
        }
    }

    /// <summary>
    /// Starting points, not measured numbers - the same caveat every resilience default in this
    /// codebase carries (`ago-chat`'s own <c>ConfigureReplyDraftResilienceDefaults</c>). Closer to that
    /// method's own shape than to a background job's: an operator or visitor is watching this call
    /// resolve synchronously. Only <c>Timeout</c>/<c>Bulkhead</c> are set - see
    /// <see cref="OpenAiCompatibleFaqAnswerResiliencePipeline"/>'s own remarks for why Retry/CircuitBreaker
    /// are deliberately absent here.
    /// </summary>
    private static void ConfigureFaqAnswerResilienceDefaults(ResiliencePipelineOptions options)
    {
        options.Timeout = new ResilienceTimeoutOptions { Duration = TimeSpan.FromSeconds(8) };
        options.Bulkhead = new ResilienceBulkheadOptions { MaxConcurrency = 4, MaxQueuedActions = 16 };
    }
}
