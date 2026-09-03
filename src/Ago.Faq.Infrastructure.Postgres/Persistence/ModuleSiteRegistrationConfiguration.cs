using Ago.Faq.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ago.Faq.Infrastructure.Postgres.Persistence;

internal sealed class ModuleSiteRegistrationConfiguration : IEntityTypeConfiguration<ModuleSiteRegistration>
{
    public void Configure(EntityTypeBuilder<ModuleSiteRegistration> builder)
    {
        builder.ToTable("module_site_registrations");

        // SiteId is the primary key, not a surrogate id - the identical call
        // KnowledgeBaseConfiguration's own remarks make for the same reason: this is one row per
        // SiteId by construction (Domain.ModuleSiteRegistration.Register takes no id of its own to
        // generate), and a surrogate id would only add a second, redundant unique index.
        builder.HasKey(r => r.SiteId);
        builder.Property(r => r.SiteId).HasColumnName("site_id").HasConversion(IdConverters.Site).ValueGeneratedNever();

        builder.Property(r => r.Credential)
            .HasColumnName("credential")
            .HasMaxLength(ModuleCredential.MaxLength)
            .HasConversion(c => c.Value, v => new ModuleCredential(v))
            .IsRequired();

        builder.Property(r => r.RegisteredAt).HasColumnName("registered_at").HasColumnType("timestamptz");
    }
}
