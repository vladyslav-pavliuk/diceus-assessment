using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CauseOfLossCodeEntity = ClaimsModule.Domain.ReferenceData.CauseOfLossCode;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>One per claim, so the FK to Claims has a unique index (D-13).</summary>
internal sealed class LossEventConfiguration : IEntityTypeConfiguration<LossEvent>
{
    public void Configure(EntityTypeBuilder<LossEvent> builder)
    {
        builder.ToTable("LossEvents");
        builder.HasKey(lossEvent => lossEvent.Id);

        builder.Property(lossEvent => lossEvent.LossDate).IsRequired();
        builder.Property(lossEvent => lossEvent.LossDescription).IsRequired();
        builder.Property(lossEvent => lossEvent.LossLocation).HasMaxLength(FieldLengths.LossLocation);
        builder.Property(lossEvent => lossEvent.CauseOfLossCode).HasMaxLength(FieldLengths.ShortCode).IsRequired();
        builder.Property(lossEvent => lossEvent.ReportDate).IsRequired();
        builder.Property(lossEvent => lossEvent.PoliceReportNumber).HasMaxLength(FieldLengths.PoliceReportNumber);

        // Keyed on (OrganisationId, Code), so a claim can only use its own organisation's codes (BR-C-05 backstop).
        builder.HasOne<CauseOfLossCodeEntity>()
            .WithMany()
            .HasForeignKey(ShadowColumns.OrganisationId, nameof(LossEvent.CauseOfLossCode))
            .HasPrincipalKey(ShadowColumns.OrganisationId, nameof(CauseOfLossCodeEntity.Code))
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex([nameof(LossEvent.LossDate)], "IX_LossEvents_LossDate");
    }
}
