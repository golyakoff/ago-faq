using Ago.Platform.Kernel;

namespace Ago.Faq.Application.UseCases.KnowledgeBase;

public static class KnowledgeBaseErrors
{
    /// <summary>Checked here, in Application, before <c>Ago.Faq.Domain.KnowledgeBase</c>'s own
    /// constructor guard ever runs - the same "an operator can act on this" reasoning
    /// <c>Ago.Calendar.Api.Configuration.ConsoleEndpoints</c>'s own day-of-week check makes for a
    /// caller-supplied value: a console user who pastes too much text gets an ordinary, actionable
    /// 400, not a 500 from an exception escaping the domain's own defensive guard.
    ///
    /// <para>Qualified as <c>Domain.KnowledgeBase</c> in the handler that calls this, not
    /// <c>using</c>-imported bare - this file's own namespace shares its last segment with the Domain
    /// type's name, the identical CS0118 collision <c>ReplyToFaqModuleTaskHandler</c>'s own remarks
    /// describe for <c>FaqModuleTask</c>.</para></summary>
    public static Error TextTooLong() => new(
        "knowledge_base.text_too_long",
        $"Knowledge-base text must be at most {Domain.KnowledgeBase.MaxTextLength} characters.");
}
