namespace Ago.Faq.Infrastructure.Postgres;

/// <summary>`22-11`: bound from `ModuleProvisioning:Secret` - the identical shape
/// <c>Ago.Calendar.Infrastructure.Postgres.ModuleProvisioningOptions</c> gives its sibling. This
/// module's own independently generated value - not the same secret as the calendar deployment's, the
/// same "a value this row and the module's own per-tenant registry row must be configured to match"
/// per-deployment coordination `secrets.md` already tracks for the per-call credential.</summary>
public sealed class ModuleProvisioningOptions
{
    public const string SectionName = "ModuleProvisioning";

    public string Secret { get; set; } = string.Empty;
}
