using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
    {
        public void Configure(EntityTypeBuilder<JournalEntry> builder)
        {
            builder.ToTable("JournalEntries");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
            builder.Property(e => e.SourceModule).HasMaxLength(50);

            builder.HasIndex(e => new { e.TenantId, e.Date });
            builder.HasIndex(e => new { e.TenantId, e.SourceModule, e.SourceDocumentId });

            builder.HasMany(e => e.Lines)
                .WithOne(l => l.JournalEntry)
                .HasForeignKey(l => l.JournalEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
