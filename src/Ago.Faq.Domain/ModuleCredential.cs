namespace Ago.Faq.Domain;

/// <summary>
/// `22-04`: the secret half of a <see cref="ModuleSiteRegistration"/> row - "site X's chat module
/// calls are proven by this credential." A second, independent copy of the value-object shape
/// `Ago.Chat.Domain.ModuleCredential` already establishes on the minting side (no shared package
/// between products; see that type's own remarks) - restated here rather than referenced because each
/// product's own domain types are its own (coding-style.md), the same call
/// `Ago.Faq.Domain.SiteId`'s own remarks make for id types.
///
/// <para><b>Opaque to this module beyond shape.</b> Validates non-empty and bounded length only, never
/// entropy - the identical honesty `Ago.Chat.Domain.ModuleCredential.MinLength`'s own remarks state for
/// its floor.</para>
/// </summary>
public readonly record struct ModuleCredential
{
    public const int MinLength = 16;

    public const int MaxLength = 256;

    public ModuleCredential(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A module credential cannot be empty.", nameof(value));
        }

        var trimmed = value.Trim();
        if (trimmed.Length < MinLength)
        {
            throw new ArgumentException(
                $"A module credential must be at least {MinLength} characters long.", nameof(value));
        }

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException(
                $"A module credential cannot exceed {MaxLength} characters.", nameof(value));
        }

        Value = trimmed;
    }

    public string Value { get; }

    // Deliberately not overridden to print the secret - the same reason
    // Ago.Chat.Domain.ModuleCredential's own ToString() gives.
    public override string ToString() => "ModuleCredential(***)";
}
