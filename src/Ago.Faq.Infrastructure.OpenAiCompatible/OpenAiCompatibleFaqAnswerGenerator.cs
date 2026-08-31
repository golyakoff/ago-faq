using System.Net.Http.Json;
using Ago.Faq.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Ago.Faq.Infrastructure.OpenAiCompatible;

/// <summary>
/// The one class in this codebase that speaks the OpenAI Chat Completions HTTP shape - a plain typed
/// <see cref="HttpClient"/>, no LLM SDK package, the same "thin adapter, resilience is the composition
/// root's job" discipline `ago-chat`'s own <c>YandexGptReplyDraftClient</c> establishes. Implements
/// <see cref="IFaqAnswerGenerator"/>, the provider-neutral Application port; nothing above this
/// project may reference this class or any type in this file directly.
///
/// <para><b>Deliberately different from <c>YandexGptReplyDraftClient</c>: this class never throws.</b>
/// That client's terminal-vs-transient exception split exists so its own resilience decorator can
/// choose what to retry. This module has no such distinction to make - an FAQ answer that fails for
/// any reason (network fault, timeout, a non-2xx status, a malformed or missing response body) all
/// resolve to the identical <see cref="FaqAnswerResult.Unavailable"/>, which
/// <c>AnswerFaqQuestionHandler</c> already maps to the low-confidence escalation regardless of which
/// specific thing went wrong - there is no second, different UI reaction a terminal-vs-transient split
/// would enable here. Catching everything at this boundary also means an operator or visitor waiting
/// synchronously on this call can never see an unhandled exception surface past it.</para>
///
/// <para><b>Consequence for the resilience pipeline wrapping this class
/// (<c>Ago.Faq.Module.OpenAiCompatibleFaqAnswerResiliencePipeline</c>).</b> Because this adapter never
/// throws, <c>Ago.Platform.Resilience.ResiliencePolicyBuilder.WithRetry</c>/<c>WithCircuitBreaker</c> -
/// both built on an <c>Exception</c> predicate (verified by reading that package's own source, not
/// assumed) - would never see a fault to react to if configured here, so this module's own pipeline
/// configures only <c>WithTimeout</c> (Polly's timeout races the call independently of what the
/// delegate itself returns, so a hang is still caught) and <c>WithBulkhead</c> (admission control,
/// independent of any exception too). Configuring Retry/CircuitBreaker on a delegate that cannot throw
/// would be presenting protection that structurally cannot trigger - named here rather than shipped
/// silently.</para>
///
/// <para><b>Not confirmed against a real provider response.</b> No real OpenAI-compatible API key
/// exists in any environment this project runs in yet - the same honest limitation
/// <c>YandexGptReplyDraftClient</c>'s own remarks state for its provider. This class's parsing logic is
/// proven against a fake <see cref="HttpMessageHandler"/> in
/// <c>Ago.Faq.Infrastructure.OpenAiCompatible.Tests</c>/<c>Ago.Faq.Application.Tests</c> instead.</para>
/// </summary>
public sealed class OpenAiCompatibleFaqAnswerGenerator(HttpClient httpClient, IOptions<OpenAiCompatibleFaqAnswerOptions> options)
    : IFaqAnswerGenerator
{
    /// <summary>The exact literal token the system prompt instructs the model to answer with when the
    /// knowledge base does not cover the question. Compared case-insensitively after trimming - a
    /// model that adds trailing punctuation or different casing still counts.</summary>
    private const string NotFoundToken = "NOT_FOUND";

    public async Task<FaqAnswerResult> AnswerAsync(FaqAnswerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var opts = options.Value;
            var systemPrompt = BuildSystemPrompt(request.KnowledgeBaseText);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(new ChatCompletionRequest(
                    Model: opts.Model,
                    Messages: [new ChatMessage("system", systemPrompt), new ChatMessage("user", request.Question)],
                    Temperature: 0.0,
                    MaxTokens: opts.MaxTokens)),
            };

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new FaqAnswerResult.Unavailable(
                    $"The FAQ answer provider returned {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken);
            var content = body?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                return new FaqAnswerResult.NotGrounded(
                    "The knowledge base does not cover this question.");
            }

            var trimmed = content.Trim();
            if (string.Equals(trimmed, NotFoundToken, StringComparison.OrdinalIgnoreCase))
            {
                return new FaqAnswerResult.NotGrounded(
                    "The knowledge base does not cover this question.");
            }

            return new FaqAnswerResult.Answered(trimmed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller's own cancellation (the visitor navigated away, the request was aborted) -
            // propagates rather than degrading, the same "cancellation is not this call's business"
            // rule ResilientReplyDraftGenerator's own remarks state: the caller is gone and will not
            // read either outcome.
            throw;
        }
        catch (Exception ex)
        {
            // Every other failure this call can produce - a network fault, a timeout that did not
            // originate from the caller's own token, an unparseable response body - degrades to the
            // identical answer. See this class's own remarks for why there is no terminal/transient
            // split to make here.
            return new FaqAnswerResult.Unavailable(BuildUnavailableReason(ex));
        }
    }

    private static string BuildUnavailableReason(Exception ex) =>
        $"The FAQ answer provider is temporarily unavailable ({ex.GetType().Name}).";

    /// <summary>
    /// The entire "minimal framing" half of this call - no site name, no tenant policy, no product
    /// catalog beyond the knowledge-base text itself, matching `19-01`'s own context-minimalism rule
    /// (<c>YandexGptReplyDraftClient</c>'s own remarks) applied to grounded question-answering instead
    /// of reply drafting. The knowledge-base text is embedded directly in the system message - at this
    /// module's own "a few paragraphs" scale there is no retrieval step to design (`19-03`'s own
    /// "Decided" section, ago-root).
    /// </summary>
    private static string BuildSystemPrompt(string knowledgeBaseText) =>
        "You are answering a customer's question using only the knowledge base below. If the answer " +
        "is not contained in the knowledge base, or the question is unrelated to it, reply with the " +
        $"exact text {NotFoundToken} and nothing else - no punctuation, no explanation. Otherwise, " +
        "answer briefly and politely, using only facts from the knowledge base, in the same language " +
        "the question is written in.\n\nKnowledge base:\n" + knowledgeBaseText;
}
