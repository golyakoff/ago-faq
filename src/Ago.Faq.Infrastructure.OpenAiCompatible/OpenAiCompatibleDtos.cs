using System.Text.Json.Serialization;

namespace Ago.Faq.Infrastructure.OpenAiCompatible;

/// <summary>
/// The OpenAI Chat Completions request/response shape - the same JSON contract every OpenAI-compatible
/// provider (a hosted OpenAI deployment, or any of the many self-hosted gateways that mimic its
/// <c>/chat/completions</c> route) speaks, which is the entire reason this client is named
/// "OpenAiCompatible" rather than after one specific vendor. Hand-rolled DTOs plus
/// <c>System.Net.Http.Json</c>, the same "no LLM SDK package" convention every provider client in
/// `ago-chat` already follows (<c>YandexGptReplyDraftClient</c>'s own remarks).
///
/// <para><b>Not confirmed against a real provider response</b> - see
/// <see cref="OpenAiCompatibleFaqAnswerGenerator"/>'s own remarks for why: no real API key exists in
/// any environment this project runs in yet.</para>
/// </summary>
internal sealed record ChatCompletionRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
    [property: JsonPropertyName("temperature")] double Temperature,
    [property: JsonPropertyName("max_tokens")] int MaxTokens);

internal sealed record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal sealed record ChatCompletionResponse(
    [property: JsonPropertyName("choices")] IReadOnlyList<ChatCompletionChoice>? Choices);

internal sealed record ChatCompletionChoice(
    [property: JsonPropertyName("message")] ChatMessage? Message);
