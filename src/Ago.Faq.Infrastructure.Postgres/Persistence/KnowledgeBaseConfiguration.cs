using Ago.Faq.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ago.Faq.Infrastructure.Postgres.Persistence;

internal sealed class KnowledgeBaseConfiguration : IEntityTypeConfiguration<KnowledgeBase>
{
    public void Configure(EntityTypeBuilder<KnowledgeBase> builder)
    {
        builder.ToTable("knowledge_bases");

        // SiteId is the primary key, not a surrogate id: this module's own "Decided, 2026-08-31"
        // section (19-03's backlog file, ago-root) fixes the shape at "one row per SiteId", and a
        // surrogate id would only invite a second, redundant unique index doing the same job.
        builder.HasKey(k => k.SiteId);
        builder.Property(k => k.SiteId).HasColumnName("site_id").HasConversion(IdConverters.Site).ValueGeneratedNever();

        // KnowledgeBase.MaxTextLength (8000) as the column bound too - the domain's own invariant,
        // restated at the storage boundary rather than left implicit; a column with no limit would
        // let a row bypass the domain guard entirely if anything ever wrote here through raw SQL.
        builder.Property(k => k.Text).HasColumnName("text").HasMaxLength(KnowledgeBase.MaxTextLength).IsRequired();

        builder.Property(k => k.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
    }
}
