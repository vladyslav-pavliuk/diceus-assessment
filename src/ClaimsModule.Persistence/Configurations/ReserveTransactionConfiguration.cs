using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>ReserveHistory. ImmutableRowsInterceptor guards the amount columns (D-22).</summary>
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
        // A concurrency token, so a tracked update (a retry, a rejection) is a compare-and-set too: it fails with 409
        // rather than overwrite a status the GL job changed after it was read.
        builder.Property(transaction => transaction.PostingStatus).IsRequired().IsConcurrencyToken();
        builder.Property(transaction => transaction.PostingJobId).HasMaxLength(100);
        builder.Property(transaction => transaction.IdempotencyKey).HasMaxLength(GlIdempotencyKey.MaxLength).IsRequired();
        builder.Property(transaction => transaction.ChangeSequence).IsRequired();
        builder.Property(transaction => transaction.SubmittedByUserId).IsRequired();

        builder.HasOne<Claim>().WithMany().HasForeignKey(transaction => transaction.ClaimId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex([nameof(ReserveTransaction.ReserveComponentId), nameof(ReserveTransaction.ChangeSequence)], "UX_ReserveHistory_ReserveComponentId_ChangeSequence")
            .IsUnique();

        // BR-R-06 backstop for the GL job.
        builder.HasIndex([nameof(ReserveTransaction.IdempotencyKey)], "UX_ReserveHistory_IdempotencyKey").IsUnique();

        // One pending transaction per component, enforced by the database too (D-22).
        builder.HasIndex([nameof(ReserveTransaction.ReserveComponentId)], "UX_ReserveHistory_ReserveComponentId_Pending")
            .IsUnique()
            .HasFilter("[ApprovalStatus] = N'PendingApproval' AND [IsDeleted] = 0");

        builder.HasIndex([nameof(ReserveTransaction.ClaimId), ShadowColumns.CreatedAt], "IX_ReserveHistory_ClaimId_CreatedAt");

        // For the GL sweeper (D-15).
        builder.HasIndex(
            [nameof(ReserveTransaction.ApprovalStatus), nameof(ReserveTransaction.PostingStatus), nameof(ReserveTransaction.ApprovedAt)],
            "IX_ReserveHistory_ApprovalStatus_PostingStatus_ApprovedAt");
    }
}
