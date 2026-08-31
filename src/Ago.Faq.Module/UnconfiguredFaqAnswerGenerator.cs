using Ago.Faq.Application.Abstractions;

namespace Ago.Faq.Module;

/// <summary>
/// "No OpenAI-compatible credentials configured for this deployment" - the same role
/// `ago-chat`'s own <c>UnconfiguredReplyDraftGenerator</c> plays for `19-01`, restated for this
/// module's own port. No HTTP call is ever attempted; the visitor gets the identical low-confidence
/// escalation a reachable-but-unsure provider would produce, because a caller has no way to act on
/// "misconfigured" differently than "temporarily down" - either way, no answer is available right now.
/// </summary>
public sealed class UnconfiguredFaqAnswerGenerator : IFaqAnswerGenerator
{
    public Task<FaqAnswerResult> AnswerAsync(FaqAnswerRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<FaqAnswerResult>(
            new FaqAnswerResult.Unavailable("FAQ answer generation is not configured for this deployment."));
}
