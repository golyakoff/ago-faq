namespace Ago.Faq.Domain;

/// <summary>
/// One tenant's knowledge base - "a few paragraphs of text a tenant pastes into a console field", the
/// honest minimum this module's own backlog item decided on rather than a document-ingestion pipeline
/// with chunking and embeddings (`19-03`'s "Decided, 2026-08-31" section, ago-root). One row per
/// <see cref="SiteId"/>; the whole text is passed to the LLM as context on every question - no
/// retrieval step exists because none is needed at this scale.
///
/// <para><b>Why a bounded plain-text field rather than, say, a list of Q&amp;A pairs.</b> A tenant does
/// not write FAQs in a structured shape when they paste "we accept returns within 30 days, shipping
/// costs $5 outside the city, we are open 9-6 Mon-Sat" into a box - forcing structure onto that input
/// would be building a form the honest minimum shape does not need yet. If this proves too limited for
/// a real answer quality bar, that is a concrete, measured trigger for a richer format later
/// (`CLAUDE.md`'s premature-generalisation caution), not a guess made now.</para>
///
/// <para><b><see cref="MaxTextLength"/> = 8000 characters.</b> Roughly 1200-1500 words - "a few
/// paragraphs" with headroom, not "a document". The console's own PUT handler is where an
/// over-length submission is rejected as an ordinary, caller-actionable <c>Result</c> failure
/// (`knowledge_base.text_too_long`); the guard here is the same defensive second line
/// <c>ChatBookingTask</c>'s own state guard is for its caller - a bug in the caller, not a path a
/// well-behaved one should ever reach.</para>
/// </summary>
public sealed class KnowledgeBase
{
    public const int MaxTextLength = 8000;

    public SiteId SiteId { get; }

    public string Text { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private KnowledgeBase(SiteId siteId, string text, DateTimeOffset now)
    {
        SiteId = siteId;
        Text = text;
        UpdatedAt = now;
    }

    // EF Core materialization only - never called by domain code.
    private KnowledgeBase()
    {
        Text = string.Empty;
    }

    /// <summary>A tenant's first save. <paramref name="text"/> may be empty - an empty knowledge base
    /// is a real, legitimate state (the visitor gets the low-confidence escape for every question,
    /// exactly as if no row existed at all), not special-cased into an error here.</summary>
    public static KnowledgeBase Create(SiteId siteId, string text, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(text);
        RequireWithinLength(text);
        return new KnowledgeBase(siteId, text, now);
    }

    public void UpdateText(string text, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(text);
        RequireWithinLength(text);
        Text = text;
        UpdatedAt = now;
    }

    private static void RequireWithinLength(string text)
    {
        if (text.Length > MaxTextLength)
        {
            throw new ArgumentException(
                $"Knowledge-base text must be at most {MaxTextLength} characters; got {text.Length}.",
                nameof(text));
        }
    }
}
