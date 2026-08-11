using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prumo.Domain.Entities;

namespace Prumo.Infrastructure.Data.Configurations
{
    public class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
    {
        public void Configure(EntityTypeBuilder<JournalLine> builder)
        {
            builder.ToTable("JournalLines");

            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedNever();

            builder.Property(l => l.Amount).HasColumnType("decimal(18,2)");
            builder.Property(l => l.EntryType).HasConversion<int>();

            builder.HasIndex(l => l.JournalEntryId);
            builder.HasIndex(l => l.AccountId);

            builder.HasOne(l => l.Account)
                .WithMany(a => a.JournalLines)
                .HasForeignKey(l => l.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
