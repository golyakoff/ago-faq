using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;

namespace Ago.Faq.Application.UseCases.FaqModuleTask;

/// <summary>
/// The one place "turn a question into a step" happens, shared by both
/// <see cref="StartFaqModuleTaskHandler"/> (the inline-question case, <c>/faq what are your hours</c>)
/// and <see cref="ReplyToFaqModuleTaskHandler"/> (the bare-trigger case's follow-up reply) - not
/// duplicated between them, because both need the identical "empty knowledge base skips the provider,
/// otherwise call it and map the three outcomes" logic and a second copy would be the first place the
/// two silently drift apart.
///
/// <para><b>The must-test rule, stated here because this is where it is enforced.</b> If the site's
/// knowledge-base text is null, empty, or whitespace, this handler returns
/// <see cref="FaqModuleStep.Escalate"/> without ever calling <see cref="IFaqAnswerGenerator"/> - the
/// identical "no tag vocabulary configured -&gt; do nothing, never call the provider" rule
/// `ago-chat`'s own `19-02` categorization handler already established for an analogous empty-config
/// case. Calling a paid LLM API to answer from zero context would be worse than useless: it would burn
/// a real request and get back a well-formed <c>NOT_FOUND</c> that changes nothing about the outcome
/// this handler already knew for free.</para>
/// </summary>
public sealed class AnswerFaqQuestionHandler(IKnowledgeBaseRepository knowledgeBases, IFaqAnswerGenerator generator)
{
    public async Task<FaqModuleStep> HandleAsync(SiteId siteId, string question, CancellationToken cancellationToken)
    {
        var knowledgeBase = await knowledgeBases.FindBySiteIdAsync(siteId, cancellationToken);

        if (knowledgeBase is null || string.IsNullOrWhiteSpace(knowledgeBase.Text))
        {
            return new FaqModuleStep.Escalate(
                "This site has not set up its knowledge base yet - a person will help you instead.");
        }

        var result = await generator.AnswerAsync(
            new FaqAnswerRequest(knowledgeBase.Text, question), cancellationToken);

        return result switch
        {
            FaqAnswerResult.Answered answered => new FaqModuleStep.Answer(answered.AnswerText),
            FaqAnswerResult.NotGrounded notGrounded => new FaqModuleStep.Escalate(
                DefaultIfBlank(notGrounded.Reason)),
            FaqAnswerResult.Unavailable unavailable => new FaqModuleStep.Escalate(
                DefaultIfBlank(unavailable.Reason)),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown FaqAnswerResult."),
        };
    }

    /// <summary>An escalate step's prompt is optional on the wire (adr/0081) - this module always
    /// supplies one when it has any reason text at all, and falls back to a short, friendly sentence
    /// only if a provider adapter somehow returned an empty reason, so a visitor never sees a blank
    /// message where an explanation was expected.</summary>
    private static string DefaultIfBlank(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? "I couldn't find an answer to that - a person will help you instead."
            : reason;
}
