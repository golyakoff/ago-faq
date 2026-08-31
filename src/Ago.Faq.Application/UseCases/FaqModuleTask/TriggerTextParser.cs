namespace Ago.Faq.Application.UseCases.FaqModuleTask;

/// <summary>
/// Splits the full visitor message Chat hands this module on task start
/// (<c>ModuleTaskStartRequest.TriggerText</c>) into "whichever trigger word matched" and "whatever the
/// visitor typed after it". This module receives the full text, leading trigger word included - it is
/// never told separately which word matched, because Chat's own trigger registry
/// (`20-07`'s own trigger-matching mechanism, reused unchanged here) is a Chat concern, not this
/// module's.
/// </summary>
internal static class TriggerTextParser
{
    /// <summary>
    /// Strips the first whitespace-delimited token (the trigger word itself, e.g. <c>/faq</c> or
    /// <c>/помощь</c>) and trims what remains. Returns an empty string for a bare trigger with nothing
    /// after it - the caller's own signal to send the <c>form</c> step rather than attempt an answer.
    /// </summary>
    public static string ExtractQuestion(string triggerText)
    {
        ArgumentNullException.ThrowIfNull(triggerText);

        var trimmed = triggerText.Trim();
        var firstSpace = trimmed.IndexOfAny([' ', '\t', '\n', '\r']);
        if (firstSpace < 0)
        {
            return string.Empty;
        }

        return trimmed[(firstSpace + 1)..].Trim();
    }
}
