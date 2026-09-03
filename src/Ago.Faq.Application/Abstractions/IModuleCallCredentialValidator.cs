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
/// </summary>
public interface IModuleCallCredentialValidator
{
    /// <summary>Validates the raw <c>X-Ago-Module-Credential</c> header value (may be <see
    /// langword="null"/> or empty when the header was not sent). Never throws - every outcome, including
    /// "no header, and this deployment does not yet require one", is a value in
    /// <see cref="ModuleCallCredentialResult"/>.</summary>
    ModuleCallCredentialResult Validate(string? headerValue, DateTimeOffset now);
}

/// <param name="IsAuthenticated">
/// <see langword="false"/> means "refuse this call with 401" - covering both a required credential that
/// is missing and one that is present but wrong (bad signature, malformed, or expired). A credential
/// that is missing while this deployment's rollout policy has not yet made one mandatory is the one
/// case that is *not* a refusal - see the implementation's own remarks on the accepting-but-warning
/// rollout window.
/// </param>
/// <param name="SiteId">
/// The site id the credential proved, when <paramref name="IsAuthenticated"/> is <see langword="true"/>
/// and a credential was actually presented and verified. <see langword="null"/> in the one case a call
/// is authenticated with no site id to check: the accepting-but-warning window, where no header was
/// sent at all and this deployment has not yet made one mandatory. A caller that receives
/// <see langword="null"/> here skips the site cross-check rather than treating it as a match.
/// </param>
public readonly record struct ModuleCallCredentialResult(bool IsAuthenticated, Guid? SiteId);
