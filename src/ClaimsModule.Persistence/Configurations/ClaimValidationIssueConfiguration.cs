using ClaimsModule.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>ClaimValidationIssues (D-07): not an FRS §9 entity; it gives CC-02 and BR-ST-02 something to query.</summary>
internal sealed class ClaimValidationIssueConfiguration : IEntityTypeConfiguration<ClaimValidationIssue>
{
    public void Configure(EntityTypeBuilder<ClaimValidationIssue> builder)
    {
        builder.ToTable("ClaimValidationIssues");
        builder.HasKey(issue => issue.Id);

        builder.Property(issue => issue.RuleCode).HasMaxLength(50).IsRequired();
        builder.Property(issue => issue.Severity).IsRequired();
        builder.Property(issue => issue.Field).HasMaxLength(100).IsRequired();
        builder.Property(issue => issue.Message).HasMaxLength(500).IsRequired();
        builder.Property(issue => issue.Status).IsRequired();
        builder.Property(issue => issue.RaisedAt).IsRequired();
        builder.Property(issue => issue.ResolutionNote).HasMaxLength(500);

        // At most one active (Open or Acknowledged) issue per rule and claim: re-validation cannot duplicate one.
        builder.HasIndex([nameof(ClaimValidationIssue.ClaimId), nameof(ClaimValidationIssue.RuleCode)], "UX_ClaimValidationIssues_ClaimId_RuleCode_Active")
            .IsUnique()
            .HasFilter("[Status] IN (N'Open', N'Acknowledged') AND [IsDeleted] = 0");
    }
}
