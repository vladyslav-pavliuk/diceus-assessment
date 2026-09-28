using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>
/// Append-only in three layers (D-14): a single writer, an interceptor, and a trigger that also stops raw SQL.
/// </summary>
internal sealed class ClaimAuditLogConfiguration : IEntityTypeConfiguration<ClaimAuditLog>
{
    public const string AppendOnlyTrigger = "TR_ClaimAuditLog_AppendOnly";

    public void Configure(EntityTypeBuilder<ClaimAuditLog> builder)
    {
        // Declaring the trigger stops EF Core from using OUTPUT without INTO, which SQL Server refuses on tables with triggers.
        builder.ToTable("ClaimAuditLog", table => table.HasTrigger(AppendOnlyTrigger));
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.EventType).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.Description).IsRequired();
        builder.Property(entry => entry.OldValue);
        builder.Property(entry => entry.NewValue);
        builder.Property(entry => entry.RelatedEntityType).HasMaxLength(100);
        builder.Property(entry => entry.CreatedAt).IsRequired();

        builder.HasOne<Claim>().WithMany().HasForeignKey(entry => entry.ClaimId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex([nameof(ClaimAuditLog.ClaimId), nameof(ClaimAuditLog.CreatedAt)], "IX_ClaimAuditLog_ClaimId_CreatedAt")
            .IsDescending(false, true);

        // BR-R-06 backstop behind the GL job's compare-and-set: one GL_POSTING_SIMULATED per reserve transaction.
        builder.HasIndex([nameof(ClaimAuditLog.RelatedEntityId)], "UX_ClaimAuditLog_RelatedEntityId_GlPostingSimulated")
            .IsUnique()
            .HasFilter($"[EventType] = N'{AuditEventTypes.GlPostingSimulated}'");

        // The SLA job's "last breach of this claim" lookup (D-01).
        builder.HasIndex(
            [nameof(ClaimAuditLog.ClaimId), nameof(ClaimAuditLog.EventType), nameof(ClaimAuditLog.CreatedAt)],
            "IX_ClaimAuditLog_ClaimId_EventType_CreatedAt");
    }
}
