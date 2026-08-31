using Ago.Faq.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ago.Faq.Module;

/// <summary>
/// Wraps <c>Ago.Faq.Infrastructure.OpenAiCompatible.OpenAiCompatibleFaqAnswerGenerator</c> in
/// <see cref="OpenAiCompatibleFaqAnswerResiliencePipeline"/> - composition in the composition root,
/// not inheritance every implementation must remember to opt into, the same decorator shape
/// `ago-chat`'s own <c>ResilientReplyDraftGenerator</c> establishes.
///
/// <para><b>Catches even though the inner call already never throws under ordinary failure.</b> The
/// wrapping pipeline's own <c>WithTimeout</c> can still fault the execution (a
/// <c>TimeoutRejectedException</c>, thrown by Polly's own race regardless of what the inner delegate
/// returns - see <see cref="OpenAiCompatibleFaqAnswerResiliencePipeline"/>'s own remarks), and
/// <c>WithBulkhead</c> can reject before the inner delegate ever runs at all
/// (<c>RateLimiterRejectedException</c>). Both are real exceptions this decorator is the one place
/// left to catch: an operator or visitor is waiting synchronously, and there is no later retry point
/// for either to propagate to.</para>
/// </summary>
public sealed class ResilientFaqAnswerGenerator(
    IFaqAnswerGenerator inner,
    OpenAiCompatibleFaqAnswerResiliencePipeline pipeline,
    ILogger<ResilientFaqAnswerGenerator> logger) : IFaqAnswerGenerator
{
    public async Task<FaqAnswerResult> AnswerAsync(FaqAnswerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await pipeline.Pipeline.ExecuteAsync(
                async token => await inner.AnswerAsync(request, token), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller's own cancellation - propagates rather than degrading, the same rule
            // ResilientReplyDraftGenerator's own remarks state for the identical case.
            throw;
        }
        catch (Exception ex)
        {
            // A timeout or a bulkhead rejection from the pipeline itself - logged at Warning, not
            // Error: an unavailable FAQ answer degrades to the escalation step, not an incident
            // (resilience.md's own "the assertion is always about the rest of the system staying
            // healthy while the dependency is broken").
            logger.LogWarning(ex, "FAQ answer provider unavailable; degrading to the low-confidence escalation.");
            return new FaqAnswerResult.Unavailable("The FAQ answer suggestion is temporarily unavailable.");
        }
    }
}
