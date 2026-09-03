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
public readonly record struct ModuleCallCredentialResult(bool IsAuthenticated, Guid? SiteId);
