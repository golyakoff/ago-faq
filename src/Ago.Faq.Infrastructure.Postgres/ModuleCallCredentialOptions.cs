namespace Ago.Faq.Infrastructure.Postgres;

/// <summary>
/// `22-02`: bound from <c>ChatModule:*</c> - the same section name `ago-calendar`'s own
/// <c>ChatModuleTaskOptions</c>/<c>ModuleCallCredentialOptions</c> use, kept identical across both
/// products on purpose even though this one has no <c>TenantPublicKey</c>/<c>CalendarId</c> of its own
/// to sit beside: an operator configuring this deployment reaches for the same setting name regardless
/// of which module product they are pointed at.
/// </summary>
public sealed class ModuleCallCredentialOptions
{
    public const string SectionName = "ChatModule";

    /// <summary>The shared secret this deployment's own `Ago.Chat.*` registration was given - see
    /// <c>Ago.Chat.Domain.ModuleCredential</c>'s own remarks on the other side of this pairing. Empty
    /// by default, which <see cref="HmacModuleCallCredentialValidator"/> treats as "no credential
    /// configured" and therefore refuses <em>any</em> presented credential outright.</summary>
    public string SharedSecret { get; set; } = string.Empty;

    /// <summary>`22-02`'s own rollout affordance - see this item's report for the argument. Defaults to
    /// <see langword="true"/>; an operator sets this <see langword="false"/> only for the narrow window
    /// where this host's own image has rolled out before `Ago.Chat.*`'s header-sending image has, so
    /// that window does not become an outage. A <em>wrong</em> credential is refused regardless of this
    /// flag - only a missing one is affected.</summary>
    public bool RequireCredential { get; set; } = true;
}
