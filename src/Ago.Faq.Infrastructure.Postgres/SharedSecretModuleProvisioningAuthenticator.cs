using System.Security.Cryptography;
using System.Text;
using Ago.Faq.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Ago.Faq.Infrastructure.Postgres;

/// <summary>`22-11`: the implementation of <see cref="IModuleProvisioningAuthenticator"/> - the
/// identical constant-time raw-secret compare
/// <c>Ago.Calendar.Infrastructure.Postgres.SharedSecretModuleProvisioningAuthenticator</c> gives its
/// sibling; see that class's and <see cref="IModuleProvisioningAuthenticator"/>'s own remarks for the
/// full reasoning.</summary>
public sealed class SharedSecretModuleProvisioningAuthenticator(IOptions<ModuleProvisioningOptions> options)
    : IModuleProvisioningAuthenticator
{
    public bool Authenticate(string? headerValue)
    {
        var configured = options.Value.Secret;
        if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(headerValue))
        {
            return false;
        }

        var configuredBytes = Encoding.UTF8.GetBytes(configured);
        var presentedBytes = Encoding.UTF8.GetBytes(headerValue);

        return configuredBytes.Length == presentedBytes.Length
            && CryptographicOperations.FixedTimeEquals(configuredBytes, presentedBytes);
    }
}
