using ClaimsModule.Domain.Organisations;
using ClaimsModule.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>Not a domain entity, so no soft-delete, audit columns or query filter (D-14).</summary>
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

        // A second insert with the same pair is how a repeat is detected (D-24).
        builder.HasIndex(record => new { record.UserId, record.Key }, "UX_IdempotencyRecords_UserId_Key").IsUnique();

        // The retention clean-up deletes by age.
        builder.HasIndex(record => record.CreatedAt, "IX_IdempotencyRecords_CreatedAt");
    }
}
