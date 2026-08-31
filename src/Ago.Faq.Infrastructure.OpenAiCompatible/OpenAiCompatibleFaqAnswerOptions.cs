namespace Ago.Faq.Infrastructure.OpenAiCompatible;

/// <summary>
/// Bound from <c>FaqAnswer:OpenAiCompatible:*</c>. Deliberately not <c>.ValidateOnStart()</c>'d - the
/// lesson `ago-chat` already learned the hard way once (commit <c>d0b2ba6</c>, "YandexGPT degrades
/// instead of crash-looping the host"): no environment has real credentials for this yet, and a
/// per-deployment optional feature must not be able to take an entire host down at startup for every
/// other, unrelated route that host serves. <c>Ago.Faq.Module</c> checks
/// <see cref="ApiKey"/>/<see cref="BaseUrl"/> at composition time and registers
/// <c>Ago.Faq.Module.UnconfiguredFaqAnswerGenerator</c> instead of the real client when either is
/// blank - the same "degrade one feature, not the process" choice `ago-chat`'s own
/// <c>UnconfiguredReplyDraftGenerator</c>/<c>UnconfiguredConversationCategorizer</c> already make, and
/// the same reason that class lives in the composition-root project rather than here.
/// </summary>
public sealed class OpenAiCompatibleFaqAnswerOptions
{
    public const string SectionName = "FaqAnswer:OpenAiCompatible";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int MaxTokens { get; set; } = 300;
}
