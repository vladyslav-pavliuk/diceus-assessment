using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>Not an FRS entity; it gives CC-02 and BR-ST-02 something to query (D-07).</summary>
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
        builder.Property(issue => issue.ResolutionNote).HasMaxLength(FieldLengths.Reason);

        // One active issue per rule and claim, so re-validation cannot duplicate one.
        builder.HasIndex([nameof(ClaimValidationIssue.ClaimId), nameof(ClaimValidationIssue.RuleCode)], "UX_ClaimValidationIssues_ClaimId_RuleCode_Active")
            .IsUnique()
            .HasFilter("[Status] IN (N'Open', N'Acknowledged') AND [IsDeleted] = 0");
    }
}
