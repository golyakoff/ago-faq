namespace Ago.Faq.Domain;

/// <summary>
/// `22-04`: this module's own consuming half of adr/0065's registry - "site X has this module
/// enabled, proven by this credential." Before this item, whether a site's calls were trusted at all
/// was a single deployment-wide secret (<c>Ago.Faq.Infrastructure.Postgres.ModuleCallCredentialOptions.SharedSecret</c>,
/// `22-02`) with no notion of "site X specifically is enabled" - any caller holding that one secret
/// could mint a credential for any site id it liked, and this module would answer. This row is what
/// closes that: a credential is only ever checked against the one secret its own claimed site
/// registered, so a site with no row here has nothing to be checked against and is refused outright,
/// and a credential proven for site A can never be verified against site B's own secret.
///
/// <para><b>A separate entity from anything Faq already had, not a field added to
/// <see cref="KnowledgeBase"/>.</b> The two rows answer different questions with different lifecycles:
/// a knowledge base is what this module answers with, and can be created, edited or left empty by an
/// operator on the console; a registration is who is allowed to ask, provisioned once when the chat
/// side enables the module and rotated independently of whatever the knowledge base says. Folding the
/// credential onto <see cref="KnowledgeBase"/> would make "does this site have content" and "may this
/// site call us" the same fact, and they are not - a site can have a knowledge base with no
/// registration yet (mid-provisioning) or a registration with an empty knowledge base
/// (<c>AnswerFaqQuestionHandler</c>'s own "no knowledge base configured" escalation path already
/// handles that state).</para>
///
/// <para><b>No <c>ModuleKey</c> field, unlike <c>Ago.Chat.Domain.EnabledModule</c>.</b> That row lives
/// in Chat, which serves an arbitrary number of modules from one table and needs the key to tell them
/// apart. This row lives inside <c>Ago.Faq.*</c> itself, which only ever answers for the FAQ module -
/// the type name already says which module this is, the same reason
/// <c>Ago.Calendar.Domain.ChatModuleRegistration</c>'s own remarks give for its sibling.</para>
/// </summary>
public sealed class ModuleSiteRegistration
{
    public SiteId SiteId { get; }

    public ModuleCredential Credential { get; }

    public DateTimeOffset RegisteredAt { get; }

    private ModuleSiteRegistration(SiteId siteId, ModuleCredential credential, DateTimeOffset registeredAt)
    {
        SiteId = siteId;
        Credential = credential;
        RegisteredAt = registeredAt;
    }

    // EF Core materialization only - never called by domain code.
    private ModuleSiteRegistration()
    {
    }

    public static ModuleSiteRegistration Register(SiteId siteId, ModuleCredential credential, DateTimeOffset now) =>
        new(siteId, credential, now);
}
