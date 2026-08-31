using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Ago.Faq.Api.Auth;

/// <summary>
/// JWT bearer validation for the knowledge-base console endpoint - the same Keycloak-issued operator
/// token `ago-chat`'s own console already obtains, validated here rather than provisioned as a second
/// Keycloak client (`19-03`'s own "Decided" section, ago-root). Bearer validation needs only the
/// issuer's public signing keys (no shared secret), so trusting the identical issuer costs nothing new
/// to set up.
///
/// <para><b>This module has no local <c>Operator</c>/<c>Tenant</c> table</b> - unlike
/// <c>Ago.Calendar.Api.Auth.AuthenticationSetup</c>, there is no
/// <c>IClaimsTransformation</c> resolving the token's <c>sub</c> against a locally-owned
/// operators table, and no policy requiring an operator/tenant claim pair. This module only asks "is
/// this a token the shared realm actually issued" - see <c>KnowledgeBaseEndpoints</c>'s own remarks
/// for what that deliberately leaves open.</para>
///
/// <para><b>Required, and fails loudly at startup rather than at the first request</b> - the identical
/// "no fallback, no dev stub" reasoning <c>AuthenticationSetup.AuthoritySetting</c>'s own remarks give
/// in `ago-calendar`, copied here rather than re-derived.</para>
/// </summary>
public static class FaqAuthenticationSetup
{
    public const string AuthoritySetting = "Operator:Authority";

    public const string RequireHttpsMetadataSetting = "Operator:RequireHttpsMetadata";

    public static IServiceCollection AddFaqOperatorAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var authority = configuration[AuthoritySetting]
            ?? throw new InvalidOperationException(
                $"Set {AuthoritySetting} - the shared Keycloak realm the operator console's users sign " +
                "in to (adr/0022, ago-root). There is no fallback and no dev stub.");

        var requireHttps = configuration.GetValue(RequireHttpsMetadataSetting, defaultValue: true);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.RequireHttpsMetadata = requireHttps;

                // Unrenamed claim names - without this, .NET maps `sub` to a SOAP-era URI, the same
                // adr/0022-named setting Ago.Calendar.Api.Auth.AuthenticationSetup sets for the
                // identical reason (this module reads no claim off the principal today, but a future
                // one that does would otherwise silently look for a claim that is no longer there).
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    // No distinct audience is provisioned for this module (see this file's own
                    // remarks) - the same call Ago.Calendar.Api.Auth.AuthenticationSetup makes when no
                    // Operator:Audience is configured.
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };
            });

        services.AddAuthorization();

        return services;
    }
}
