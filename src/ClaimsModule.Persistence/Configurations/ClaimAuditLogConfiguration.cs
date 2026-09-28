using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>
/// ClaimAuditLog (FRS §9.8): append-only. Rejected in three layers (D-14): IAuditLogService is the only
/// writer, ImmutableRowsInterceptor refuses modified or deleted rows, and the migration adds an
/// INSTEAD OF UPDATE, DELETE trigger that raises an error for any client, including raw SQL.
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

        // GET /claims/{id}/audit: reverse-chronological pages (FRS §10.1).
        builder.HasIndex([nameof(ClaimAuditLog.ClaimId), nameof(ClaimAuditLog.CreatedAt)], "IX_ClaimAuditLog_ClaimId_CreatedAt")
            .IsDescending(false, true);

        // BR-R-06 backstop: at most one GL_POSTING_SIMULATED per reserve transaction, whatever the code does.
        // The GL job's compare-and-set already guarantees it (ARCHITECTURE-PLAN §6.1 R6); a check-then-act bug
        // would now fail with a duplicate key instead of writing a second posting.
        builder.HasIndex([nameof(ClaimAuditLog.RelatedEntityId)], "UX_ClaimAuditLog_RelatedEntityId_GlPostingSimulated")
            .IsUnique()
            .HasFilter($"[EventType] = N'{AuditEventTypes.GlPostingSimulated}'");

        // SLA job: "last SLA_BREACH_DETECTED for this claim" (D-01).
        builder.HasIndex(
            [nameof(ClaimAuditLog.ClaimId), nameof(ClaimAuditLog.EventType), nameof(ClaimAuditLog.CreatedAt)],
            "IX_ClaimAuditLog_ClaimId_EventType_CreatedAt");
    }
}
