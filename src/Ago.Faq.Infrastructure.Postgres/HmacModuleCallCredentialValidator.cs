using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ago.Faq.Application.Abstractions;

namespace Ago.Faq.Infrastructure.Postgres;

/// <summary>
/// `22-02`: the receiving half of `Ago.Chat.Infrastructure.Modules.ModuleCallCredential` - a second,
/// independent implementation of the identical wire shape `ago-calendar`'s own
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
/// <para><b>Constant-time comparison</b> (<see cref="CryptographicOperations.FixedTimeEquals"/>) and a
/// five-second clock-skew allowance on <c>exp</c>, in both directions - the identical reasoning
/// `ago-calendar`'s own copy of this class gives for both.</para>
/// </summary>
public sealed class HmacModuleCallCredentialValidator(ModuleCallCredentialOptions options)
    : IModuleCallCredentialValidator
{
    private static readonly TimeSpan ClockSkewAllowance = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ModuleCallCredentialResult Validate(string? headerValue, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(headerValue))
        {
            return new ModuleCallCredentialResult(!options.RequireCredential, SiteId: null);
        }

        if (string.IsNullOrEmpty(options.SharedSecret))
        {
            return new ModuleCallCredentialResult(IsAuthenticated: false, SiteId: null);
        }

        var parts = headerValue.Split('.');
        if (parts.Length != 2)
        {
            return new ModuleCallCredentialResult(IsAuthenticated: false, SiteId: null);
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
            return new ModuleCallCredentialResult(IsAuthenticated: false, SiteId: null);
        }

        var expectedSignature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(options.SharedSecret), Encoding.UTF8.GetBytes(encodedPayload));
        if (!CryptographicOperations.FixedTimeEquals(presentedSignature, expectedSignature))
        {
            return new ModuleCallCredentialResult(IsAuthenticated: false, SiteId: null);
        }

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(payloadBytes, JsonOptions);
        }
        catch (JsonException)
        {
            return new ModuleCallCredentialResult(IsAuthenticated: false, SiteId: null);
        }

        if (payload is null)
        {
            return new ModuleCallCredentialResult(IsAuthenticated: false, SiteId: null);
        }

        var nowSeconds = now.ToUnixTimeSeconds();
        var skewSeconds = (long)ClockSkewAllowance.TotalSeconds;
        if (nowSeconds > payload.Exp + skewSeconds || nowSeconds < payload.Iat - skewSeconds)
        {
            return new ModuleCallCredentialResult(IsAuthenticated: false, SiteId: null);
        }

        return new ModuleCallCredentialResult(IsAuthenticated: true, payload.SiteId);
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
