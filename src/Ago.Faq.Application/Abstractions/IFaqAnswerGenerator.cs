namespace Ago.Faq.Application.Abstractions;

/// <summary>
/// A narrow, single-purpose port for exactly one capability: answer one question, grounded in one
/// knowledge base, or say why not. This mirrors `ago-chat`'s own two AI features (`19-01`'s
/// <c>IReplyDraftGenerator</c>, `19-02`'s <c>IConversationCategorizer</c>) rather than a shared generic
/// <c>ILlmClient</c> - each use case gets its own interface with zero provider vocabulary in its
/// members, so a caller never sees a model name, a token count, or an HTTP concept. The alternative -
/// one generic LLM port every AI feature shares - would leak whichever provider's request/response
/// shape happened to be first through to every caller, and this codebase's own established convention
/// (confirmed by researching both `19-01` and `19-02`) already rejected that once.
/// </summary>
public interface IFaqAnswerGenerator
{
    Task<FaqAnswerResult> AnswerAsync(FaqAnswerRequest request, CancellationToken cancellationToken);
}

public sealed record FaqAnswerRequest(string KnowledgeBaseText, string Question);

/// <summary>
/// Three, and only three, possible outcomes - deliberately not a <c>Result&lt;string&gt;</c>, because
/// "the model does not know" and "the provider is unreachable" are both routed to the identical
/// low-confidence escape by the caller (<c>AnswerFaqQuestionHandler</c>), but they are not the same
/// fact and a future caller (a metrics dashboard, say) may need to tell them apart - collapsing them
/// into one <c>Error</c> now would throw that distinction away at the one place it is still known.
/// </summary>
public abstract record FaqAnswerResult
{
    public sealed record Answered(string AnswerText) : FaqAnswerResult;

    /// <summary>The model was reached and answered, but signalled it cannot answer from the supplied
    /// knowledge base (the literal <c>NOT_FOUND</c> token, or an empty response) - or the caller never
    /// invoked the model at all because the knowledge base was empty in the first place.</summary>
    public sealed record NotGrounded(string Reason) : FaqAnswerResult;

    /// <summary>The provider could not be reached, timed out, refused the request, or returned
    /// something this module could not parse.</summary>
    public sealed record Unavailable(string Reason) : FaqAnswerResult;
}
