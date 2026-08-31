using Ago.Faq.Infrastructure.OpenAiCompatible;
using Ago.Platform.Resilience;
using Polly;

namespace Ago.Faq.Module;

/// <summary>
/// The resilience wrapping <see cref="OpenAiCompatibleFaqAnswerGenerator"/> needs, built from
/// <c>Ago.Platform.Resilience</c>'s own building blocks - the same "no fourth hand-rolled Polly setup"
/// discipline `ago-chat`'s own <c>ReplyDraftResiliencePipeline</c>/<c>BillingResiliencePipeline</c>
/// establish.
///
/// <para><b>Only <c>WithTimeout</c> and <c>WithBulkhead</c> are configured - deliberately, not an
/// oversight.</b> <c>ResiliencePolicyBuilder.WithRetry</c>/<c>WithCircuitBreaker</c> are both built on
/// an <c>Exception</c> predicate (<c>Ago.Platform.Resilience.ResiliencePolicyBuilder</c>'s own source),
/// and <see cref="OpenAiCompatibleFaqAnswerGenerator"/> never throws - every failure mode it can hit
/// already resolves to <c>FaqAnswerResult.Unavailable</c> internally (that class's own remarks explain
/// why). Configuring Retry/CircuitBreaker here would be presenting protection that structurally cannot
/// trigger, because there is never an exception for either predicate to evaluate. Timeout still
/// matters: Polly's timeout strategy races the call against a deadline independently of what the
/// delegate itself returns, so a hang is still caught and turned into a fault
/// <see cref="ResilientFaqAnswerGenerator"/> catches. Bulkhead still matters too: admission control
/// does not depend on the delegate's own exception behaviour either.</para>
///
/// <para>Registered as a singleton (<see cref="FaqModule"/>) - a scoped or transient lifetime would
/// silently rebuild a fresh, un-tripped bulkhead limiter per DI scope.</para>
/// </summary>
public sealed class OpenAiCompatibleFaqAnswerResiliencePipeline
{
    public const string PipelineName = "FaqAnswer";

    private readonly Lazy<ResiliencePipeline> _pipeline;

    public OpenAiCompatibleFaqAnswerResiliencePipeline(ResiliencePipelineOptions options) =>
        _pipeline = new Lazy<ResiliencePipeline>(() => Build(options));

    public ResiliencePipeline Pipeline => _pipeline.Value;

    private static ResiliencePipeline Build(ResiliencePipelineOptions options)
    {
        var builder = new ResiliencePolicyBuilder(PipelineName);

        if (options.Bulkhead is { } bulkhead)
        {
            builder.WithBulkhead(bulkhead);
        }

        if (options.Timeout is { } timeout)
        {
            builder.WithTimeout(timeout);
        }

        return builder.Build();
    }
}
