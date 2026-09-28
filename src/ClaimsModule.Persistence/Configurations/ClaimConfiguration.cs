using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Organisations;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Domain.Users;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

internal sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("Claims");
        builder.HasKey(claim => claim.Id);

        builder.Property(claim => claim.ClaimNumber).HasMaxLength(50).IsRequired();
        builder.Property(claim => claim.PolicyNumber).HasMaxLength(50);
        builder.Property(claim => claim.ClientName).HasMaxLength(255);
        builder.Property(claim => claim.Status).IsRequired();
        builder.Property(claim => claim.Severity).IsRequired();
        builder.Property(claim => claim.ReportedDate).IsRequired();
        builder.Property(claim => claim.ClosureReason).HasMaxLength(FieldLengths.Reason);
        builder.Property(claim => claim.Notes);
        builder.Property(claim => claim.ReserveLimitOverride).IsRequired().HasDefaultValue(false);
        builder.Property(claim => claim.ReserveLimitOverrideReason).HasMaxLength(FieldLengths.Reason);

        // Concurrency for the whole aggregate: every change inside it also touches this row (AuditColumnsInterceptor).
        builder.Property<byte[]>(ShadowColumns.RowVer).IsRowVersion().IsRequired();

        builder.Ignore(claim => claim.DomainEvents);

        // Nothing is ever physically deleted, so no cascades.
        builder.HasOne(claim => claim.LossEvent).WithOne().HasForeignKey<LossEvent>(lossEvent => lossEvent.ClaimId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(claim => claim.Parties).WithOne().HasForeignKey(party => party.ClaimId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(claim => claim.RiskObjects).WithOne().HasForeignKey(riskObject => riskObject.ClaimId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(claim => claim.ValidationIssues).WithOne().HasForeignKey(issue => issue.ClaimId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(claim => claim.ReserveComponents).WithOne().HasForeignKey(component => component.ClaimId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(claim => claim.Documents).WithOne().HasForeignKey(document => document.ClaimId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Organisation>().WithMany().HasForeignKey(ShadowColumns.OrganisationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Policy>().WithMany().HasForeignKey(claim => claim.PolicyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(claim => claim.AssignedHandlerId).OnDelete(DeleteBehavior.Restrict);

        // BR-C-04: unique per organisation. Soft-deleted claims keep their number, so the index is not filtered.
        builder.HasIndex([ShadowColumns.OrganisationId, nameof(Claim.ClaimNumber)], "UX_Claims_OrganisationId_ClaimNumber").IsUnique();

        // The claims list status filter and the SLA job.
        builder.HasIndex([ShadowColumns.OrganisationId, nameof(Claim.Status), ShadowColumns.UpdatedAt], "IX_Claims_OrganisationId_Status_UpdatedAt");
    }
}
