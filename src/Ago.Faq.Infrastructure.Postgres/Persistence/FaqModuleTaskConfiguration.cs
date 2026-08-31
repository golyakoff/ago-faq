using Ago.Faq.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ago.Faq.Infrastructure.Postgres.Persistence;

internal sealed class FaqModuleTaskConfiguration : IEntityTypeConfiguration<FaqModuleTask>
{
    public void Configure(EntityTypeBuilder<FaqModuleTask> builder)
    {
        builder.ToTable("faq_module_tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasConversion(IdConverters.FaqModuleTask)
            .ValueGeneratedNever();

        // No foreign key to knowledge_bases: a task can (and typically does, for a bare `/faq`) exist
        // for a site that has never saved a knowledge base - see AnswerFaqQuestionHandler's own
        // remarks on why that is a real, answered-by-escalation state rather than an error. Enforcing
        // referential integrity here would make starting a task fail for the exact site that most
        // needs the escalation path to work.
        builder.Property(t => t.SiteId).HasColumnName("site_id").HasConversion(IdConverters.Site);

        builder.Property(t => t.State).HasColumnName("state").HasConversion<string>().HasMaxLength(24);

        // timestamptz, from IClock, never a database default (date-and-time.md rule 1 and rule 3,
        // ago-root). Npgsql maps DateTimeOffset to timestamptz by default.
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        // Every read this module performs on this table is GetByIdAsync, a single-row primary-key
        // lookup (IFaqModuleTaskStore's own remarks) - no secondary index beyond the primary key.
        // An index on site_id would only serve a query this module never runs.
    }
}
