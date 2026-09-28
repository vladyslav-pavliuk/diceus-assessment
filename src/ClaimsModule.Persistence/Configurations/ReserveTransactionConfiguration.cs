using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>
/// ReserveHistory (FRS §9.6): the append-only transaction log of reserves. The amount columns are
/// guarded against updates by ImmutableRowsInterceptor (D-22).
/// </summary>
internal sealed class ReserveTransactionConfiguration : IEntityTypeConfiguration<ReserveTransaction>
{
    public void Configure(EntityTypeBuilder<ReserveTransaction> builder)
    {
        builder.ToTable("ReserveHistory");
        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.TransactionType).IsRequired();
        builder.Property(transaction => transaction.Amount).IsRequired();
        builder.Property(transaction => transaction.PreviousBalance).IsRequired();
        builder.Property(transaction => transaction.NewBalance).IsRequired();
        builder.Property(transaction => transaction.ApprovalStatus).IsRequired();
        builder.Property(transaction => transaction.RequiredAuthority).IsRequired();
        builder.Property(transaction => transaction.ExceedsAggregateLimit).IsRequired().HasDefaultValue(false);
        builder.Property(transaction => transaction.RejectionReason);
        builder.Property(transaction => transaction.ChangeReason).HasMaxLength(FieldLengths.Reason).IsRequired();
        builder.Property(transaction => transaction.PostingStatus).IsRequired();
        builder.Property(transaction => transaction.PostingJobId).HasMaxLength(100);
        builder.Property(transaction => transaction.IdempotencyKey).HasMaxLength(GlIdempotencyKey.MaxLength).IsRequired();
        builder.Property(transaction => transaction.ChangeSequence).IsRequired();
        builder.Property(transaction => transaction.SubmittedByUserId).IsRequired();

        // Denormalised FK to the claim (FRS §9.6 "for query convenience").
        builder.HasOne<Claim>().WithMany().HasForeignKey(transaction => transaction.ClaimId).OnDelete(DeleteBehavior.Restrict);

        // ChangeSequence is unique per component (D-23).
        builder.HasIndex([nameof(ReserveTransaction.ReserveComponentId), nameof(ReserveTransaction.ChangeSequence)], "UX_ReserveHistory_ReserveComponentId_ChangeSequence")
            .IsUnique();

        // BR-R-06 backstop for the GL job: one row per idempotency key.
        builder.HasIndex([nameof(ReserveTransaction.IdempotencyKey)], "UX_ReserveHistory_IdempotencyKey").IsUnique();

        // D-22: at most one pending transaction per component, enforced by the database too.
        builder.HasIndex([nameof(ReserveTransaction.ReserveComponentId)], "UX_ReserveHistory_ReserveComponentId_Pending")
            .IsUnique()
            .HasFilter("[ApprovalStatus] = N'PendingApproval' AND [IsDeleted] = 0");

        // Reserve history of a claim, in order.
        builder.HasIndex([nameof(ReserveTransaction.ClaimId), ShadowColumns.CreatedAt], "IX_ReserveHistory_ClaimId_CreatedAt");

        // The GL sweeper: approved but not yet posted (D-15).
        builder.HasIndex(
            [nameof(ReserveTransaction.ApprovalStatus), nameof(ReserveTransaction.PostingStatus), nameof(ReserveTransaction.ApprovedAt)],
            "IX_ReserveHistory_ApprovalStatus_PostingStatus_ApprovedAt");
    }
}
