namespace Ago.Faq.Api.Cors;

/// <summary>
/// The origins the knowledge-base console screen is served from - a plain static allow-list, not
/// <c>Ago.Calendar.Api.Cors.TenantOriginCorsPolicyProvider</c>'s dynamic, per-tenant-lookup shape.
/// That provider exists for Calendar's *public, per-shop-embed* booking widget, where the allowed
/// origin set is data a tenant edits; this endpoint has exactly one caller - the `ago-console` operator
/// SPA - so a static allow-list is right-sized (`19-03`'s own "Decided" section, ago-root). Named
/// <c>Console:AllowedOrigins</c> to match `ago-chat`'s own <c>ConsoleOriginOptions</c> section key
/// exactly, even though this class's own shape (no <c>[MinLength]</c> startup validation) is simpler:
/// an empty list here means the console cannot reach this one endpoint cross-origin, not that the
/// entire host refuses to start - a smaller blast radius than `ago-chat`'s own hub-connection case,
/// which is why that class's own required-non-empty rule is not repeated here.
/// </summary>
public sealed class ConsoleOriginOptions
{
    public const string SectionName = "Console";

    public string[] AllowedOrigins { get; set; } = [];
}
