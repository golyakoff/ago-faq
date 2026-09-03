namespace Ago.Faq.Application.Abstractions;

/// <summary>
/// `22-02`: proves a call to `/api/v1/module-tasks*` is really from `Ago.Chat.*`, for the site it
/// claims - the port `ModuleTaskEndpoints`'s own remarks named as genuinely missing ("no
/// service-to-service authentication exists yet"). A port here, not a static method or an inline check
/// in the endpoint delegate, for the identical reason `Ago.Calendar.Application.Abstractions`'s own copy
/// of this interface gives: the mechanism (an HMAC-signed, short-lived assertion today) is exactly the
/// kind of decision this codebase always puts behind an interface, and it could change later without
/// <c>Ago.Faq.Api</c> changing a line.
///
/// <para><b>One contract, two independent implementations - by design, not by omission.</b> This
/// interface and <c>HmacModuleCallCredentialValidator</c>'s own concrete shape are a second, hand-kept
/// copy of the identical wire format `ago-calendar`'s own pair implement, the same "no shared package
/// between two products" situation every other cross-repository contract in this project already
/// accepts (adr/0012 sets no precedent for one). If you are changing this file, the identical change
/// belongs in `ago-calendar`'s own copy too.</para>
///
/// <para><b>`22-04`: async, not synchronous.</b> Validating a credential now means resolving which
/// site's own secret to check it against - <see cref="Ago.Faq.Domain.ModuleSiteRegistration"/>, a
/// database read - before the signature itself can be verified. Before this item, the whole deployment
/// checked against one secret from configuration and the method never needed to await anything; that
/// shortcut disappears with the deployment-wide secret it depended on.</para>
/// </summary>
public interface IModuleCallCredentialValidator
{
    /// <summary>Validates the raw <c>X-Ago-Module-Credential</c> header value (may be <see
    /// langword="null"/> or empty when the header was not sent). Never throws - every outcome is a
    /// value in <see cref="ModuleCallCredentialResult"/>.</summary>
    Task<ModuleCallCredentialResult> ValidateAsync(string? headerValue, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <param name="IsAuthenticated">
/// <see langword="false"/> means "refuse this call with 401" - a missing header, a malformed one, a bad
/// signature, an expired one, or one naming a site with no <see cref="Ago.Faq.Domain.ModuleSiteRegistration"/>
/// of its own are all this outcome. `22-04` removes the one case that used to be exempt (an absent
/// header treated as pre-migration traffic): per-site resolution has no deployment-wide tenant left to
/// fall back to, so a call with nothing to authenticate has nothing to resolve into either, and always
/// refuses.
/// </param>
/// <param name="SiteId">
/// The site id the credential proved, whenever <paramref name="IsAuthenticated"/> is
/// <see langword="true"/> - always present in that case now that there is no header-optional rollout
/// window left (see <see cref="IsAuthenticated"/>'s own remarks).
/// </param>
/// <param name="Reason">
/// `22-12`: <see langword="null"/> exactly when <paramref name="IsAuthenticated"/> is
/// <see langword="true"/>; otherwise which of <see cref="ModuleCallRefusalReason"/>'s cases produced
/// the refusal - never serialized to the caller (<c>ModuleTaskEndpoints</c> still answers a flat
/// <c>401</c> regardless of which value this carries, adr/0099's own decision), read only by
/// <c>HmacModuleCallCredentialValidator</c> itself to log a refusal distinguishably. The identical
/// addition <c>Ago.Calendar.Application.Abstractions.ModuleCallCredentialResult</c>'s own copy makes
/// for its sibling.
/// </param>
public readonly record struct ModuleCallCredentialResult(bool IsAuthenticated, Guid? SiteId, ModuleCallRefusalReason? Reason = null);

/// <summary>
/// `22-12`/adr/0099: why a module call was refused - a second, independent copy of
/// <c>Ago.Calendar.Application.Abstractions.ModuleCallRefusalReason</c>'s own cases, the identical
/// "no shared package between two products" situation this file's own remarks already accept for the
/// rest of this contract. If you are changing this enum, the identical change belongs in
/// `ago-calendar`'s own copy too.
///
/// <para><b>Ordered roughly by how the item's own table names them</b>: <see cref="SiteNotRegistered"/>
/// is the configuration case ("the module was never enabled for it"); <see cref="InvalidSignature"/> is
/// the attack-or-mismatch case ("forged, or signed with another site's secret");
/// <see cref="AssertionExpired"/> is the assertion's own 60-second TTL running out
/// (<c>HmacModuleCallCredentialValidator</c>'s own <c>exp</c> check); <see cref="CredentialRotatedOut"/>
/// is `22-11`'s addition - a signature that verifies against the site's own <em>previous</em> secret,
/// after that secret's rotation grace window has elapsed, distinguished from <see cref="InvalidSignature"/>
/// precisely because it was never forged, only late. <see cref="NoCredential"/> and
/// <see cref="Malformed"/> are not named in the item's own table - a call with no site to attribute
/// (nothing parsed yet) - and are logged at a quieter level for exactly that reason.</para>
/// </summary>
public enum ModuleCallRefusalReason
{
    /// <summary>No <c>X-Ago-Module-Credential</c> header was sent at all.</summary>
    NoCredential,

    /// <summary>A header was sent but is not two base64url segments carrying valid JSON - never far
    /// enough into the token to know which site it claims.</summary>
    Malformed,

    /// <summary>The payload names a site with no <see cref="Ago.Faq.Domain.ModuleSiteRegistration"/>
    /// row - "the module was never enabled for it", the item's own configuration case.</summary>
    SiteNotRegistered,

    /// <summary>The payload names a registered site, but the presented signature matches none of that
    /// site's currently active credentials (and does not match a rotated-out previous one either - see
    /// <see cref="CredentialRotatedOut"/>) - forged, or signed with the wrong site's secret.</summary>
    InvalidSignature,

    /// <summary>The signature matches a currently active credential, but the payload's own <c>iat</c>/
    /// <c>exp</c> falls outside the 60-second TTL plus clock-skew allowance - the assertion itself has
    /// expired, not the site's stored credential.</summary>
    AssertionExpired,

    /// <summary>`22-11`: the signature matches the site's <em>previous</em> credential, but
    /// <see cref="Ago.Faq.Domain.ModuleSiteRegistration.PreviousCredentialExpiresAt"/> has already
    /// passed - a credential that was genuinely valid until a rotation's grace window elapsed, not a
    /// forgery.</summary>
    CredentialRotatedOut,
}
