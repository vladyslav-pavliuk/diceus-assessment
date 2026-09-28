using ClaimsModule.Domain.Organisations;
using ClaimsModule.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>IdempotencyRecords (D-24, D-39 item 18). Not a domain entity: no soft-delete, audit columns or query filter (D-14).</summary>
internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedNever();

        builder.Property(record => record.Key).HasMaxLength(IdempotencyRecord.KeyMaxLength).IsRequired();
        builder.Property(record => record.Method).HasMaxLength(10).IsRequired();
        builder.Property(record => record.Route).HasMaxLength(2000).IsRequired();
        builder.Property(record => record.RequestHash).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(record => record.ResponseLocation).HasMaxLength(2000);
        builder.Property(record => record.CreatedAt).IsRequired();

        builder.HasOne<Organisation>().WithMany().HasForeignKey(record => record.OrganisationId).OnDelete(DeleteBehavior.Restrict);

        // One request per key and user (D-24); a second insert with the same pair is how a repeat is detected.
        builder.HasIndex(record => new { record.UserId, record.Key }, "UX_IdempotencyRecords_UserId_Key").IsUnique();

        // The retention clean-up (24h, Phase 4) deletes by age.
        builder.HasIndex(record => record.CreatedAt, "IX_IdempotencyRecords_CreatedAt");
    }
}
