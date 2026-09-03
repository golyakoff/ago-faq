using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;
using Microsoft.Extensions.Logging;

namespace Ago.Faq.Infrastructure.Postgres;

/// <summary>
/// `22-02`/`22-04`: the receiving half of `Ago.Chat.Infrastructure.Modules.ModuleCallCredential` - a
/// second, independent implementation of the identical wire shape `ago-calendar`'s own
/// <c>HmacModuleCallCredentialValidator</c> implements (no shared package between products; see this
/// interface's own remarks). If you are changing this file, the identical change belongs in
/// `ago-calendar`'s own copy too.
///
/// <list type="bullet">
/// <item>Header: <c>X-Ago-Module-Credential</c>.</item>
/// <item>Token: <c>{base64url(payload JSON)}.{base64url(HMAC-SHA256(secret, UTF8(that base64url
/// string)))}</c> - the signature covers the transmitted first segment's own bytes, never a
/// re-serialization.</item>
/// <item>Payload: <c>{"siteId":"&lt;guid&gt;","iat":&lt;unix seconds&gt;,"exp":&lt;unix seconds&gt;}</c>.</item>
/// </list>
///
/// <para><b>`22-04`: the secret is per site, not per deployment.</b> Before this item, one
/// configured <c>ChatModule:SharedSecret</c> verified every call this deployment ever received,
/// regardless of which site it claimed - adr/0094's own named limit ("whoever holds the raw secret can
/// mint one for any site that deployment serves"). This class now reads the payload's claimed site id
/// <b>before</b> it can know which secret to check the signature against - the payload itself is not
/// trusted yet at that point, only used as a lookup key - and only a signature that verifies against
/// that exact site's own <see cref="ModuleSiteRegistration.Credential"/> is ever accepted. A token
/// forged for site A by copying a genuine one and editing the site id to B fails here: the signature
/// was computed with A's secret, and B's row (if one exists at all) holds a different one.</para>
///
/// <para><b>Constant-time comparison</b> (<see cref="CryptographicOperations.FixedTimeEquals"/>) and a
/// five-second clock-skew allowance on <c>exp</c>, in both directions - the identical reasoning
/// `ago-calendar`'s own copy of this class gives for both, unchanged by this item.</para>
///
/// <para><b>`22-12`/adr/0099: every refusal is classified and logged before it returns.</b> The wire
/// answer stays the flat <c>401</c> <c>ModuleTaskEndpoints</c> always returned -
/// <see cref="ModuleCallRefusalReason"/> is never serialized - but each branch below now logs which
/// case it was, structured on <c>{Reason}</c> and (whenever the payload parsed far enough to name one)
/// <c>{ClaimedSiteId}</c>, and nothing else: never the header, never a signature, never a secret. The
/// identical addition `ago-calendar`'s own copy of this class makes.</para>
/// </summary>
public sealed class HmacModuleCallCredentialValidator(
    IModuleSiteRegistrationRepository registrations, ILogger<HmacModuleCallCredentialValidator> logger)
    : IModuleCallCredentialValidator
{
    private static readonly TimeSpan ClockSkewAllowance = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ModuleCallCredentialResult> ValidateAsync(
        string? headerValue, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // `22-04`: no accepting-but-warning window left. Per-site resolution has no deployment-wide
        // tenant to fall back to, so a call with nothing to authenticate has nothing to resolve into -
        // see IModuleCallCredentialValidator's own remarks.
        if (string.IsNullOrEmpty(headerValue))
        {
            return Refuse(ModuleCallRefusalReason.NoCredential, claimedSiteId: null);
        }

        var parts = headerValue.Split('.');
        if (parts.Length != 2)
        {
            return Refuse(ModuleCallRefusalReason.Malformed, claimedSiteId: null);
        }

        var encodedPayload = parts[0];
        byte[] presentedSignature;
        byte[] payloadBytes;
        try
        {
            presentedSignature = Base64Url.Decode(parts[1]);
            payloadBytes = Base64Url.Decode(encodedPayload);
        }
        catch (FormatException)
        {
            return Refuse(ModuleCallRefusalReason.Malformed, claimedSiteId: null);
        }

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(payloadBytes, JsonOptions);
        }
        catch (JsonException)
        {
            return Refuse(ModuleCallRefusalReason.Malformed, claimedSiteId: null);
        }

        if (payload is null)
        {
            return Refuse(ModuleCallRefusalReason.Malformed, claimedSiteId: null);
        }

        // The claimed site id, not yet trusted - only used to find which secret this signature must
        // verify against. A site with no registration here has no secret to check anything against,
        // which is exactly "the module is not enabled for this site": refused, not a deployment fault.
        var registration = await registrations.GetBySiteIdAsync(new SiteId(payload.SiteId), cancellationToken);
        if (registration is null)
        {
            return Refuse(ModuleCallRefusalReason.SiteNotRegistered, payload.SiteId);
        }

        // `22-11`: tries every credential this row currently honours, current and (for a grace
        // window after a rotation) previous - see ModuleSiteRegistration.ActiveCredentials's own
        // remarks. The identical addition Ago.Calendar's own copy of this class makes.
        var verified = registration.ActiveCredentials(now).Any(candidate => SignatureMatches(candidate, encodedPayload, presentedSignature));
        if (!verified)
        {
            // `22-12`: before answering "forged", ask whether this is instead `22-11`'s own fourth
            // case - a signature that matches the site's *previous* credential, checked here
            // regardless of whether its grace window has already closed (unlike ActiveCredentials
            // above, which only ever yields it while still open). Never accepted as authentication -
            // only asked to tell "late" apart from "wrong" for the log line below.
            var reason = registration.PreviousCredential is { } previous
                && SignatureMatches(previous, encodedPayload, presentedSignature)
                    ? ModuleCallRefusalReason.CredentialRotatedOut
                    : ModuleCallRefusalReason.InvalidSignature;
            return Refuse(reason, payload.SiteId);
        }

        var nowSeconds = now.ToUnixTimeSeconds();
        var skewSeconds = (long)ClockSkewAllowance.TotalSeconds;
        if (nowSeconds > payload.Exp + skewSeconds || nowSeconds < payload.Iat - skewSeconds)
        {
            return Refuse(ModuleCallRefusalReason.AssertionExpired, payload.SiteId);
        }

        return new ModuleCallCredentialResult(IsAuthenticated: true, payload.SiteId);
    }

    private static bool SignatureMatches(ModuleCredential candidate, string encodedPayload, byte[] presentedSignature)
    {
        var expectedSignature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(candidate.Value), Encoding.UTF8.GetBytes(encodedPayload));
        return CryptographicOperations.FixedTimeEquals(presentedSignature, expectedSignature);
    }

    /// <summary>`22-12`: the one place every refusal leaves through - logs <paramref name="reason"/>
    /// and, when known, <paramref name="claimedSiteId"/>, then returns the unauthenticated result. The
    /// identical Debug/Warning split `Ago.Calendar`'s own copy of this class makes: <see cref="ModuleCallRefusalReason.NoCredential"/>
    /// and <see cref="ModuleCallRefusalReason.Malformed"/> at <see cref="LogLevel.Debug"/> - neither
    /// names a site anything downstream could act on - and every other reason at
    /// <see cref="LogLevel.Warning"/>.</summary>
    private ModuleCallCredentialResult Refuse(ModuleCallRefusalReason reason, Guid? claimedSiteId)
    {
        if (reason is ModuleCallRefusalReason.NoCredential or ModuleCallRefusalReason.Malformed)
        {
            logger.LogDebug("Module call refused: {Reason}", reason);
        }
        else
        {
            logger.LogWarning("Module call refused: {Reason} for site {ClaimedSiteId}", reason, claimedSiteId);
        }

        return new ModuleCallCredentialResult(IsAuthenticated: false, SiteId: null, reason);
    }

    private sealed record Payload(
        [property: JsonPropertyName("siteId")] Guid SiteId,
        [property: JsonPropertyName("iat")] long Iat,
        [property: JsonPropertyName("exp")] long Exp);
}

/// <summary>RFC 4648 §5 base64url, without padding - the identical four-line hand-rolled helper both
/// `ago-chat`'s minting side and `ago-calendar`'s own validator use (no shared package to put a single
/// copy in).</summary>
internal static class Base64Url
{
    public static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        var padding = (4 - (padded.Length % 4)) % 4;
        return Convert.FromBase64String(padded + new string('=', padding));
    }
}
