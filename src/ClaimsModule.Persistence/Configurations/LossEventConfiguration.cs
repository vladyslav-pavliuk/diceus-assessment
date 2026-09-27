using ClaimsModule.Domain.Claims;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CauseOfLossCodeEntity = ClaimsModule.Domain.ReferenceData.CauseOfLossCode;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>LossEvents (FRS §9.2). One per claim: the FK to Claims gets a unique index (D-13).</summary>
internal sealed class LossEventConfiguration : IEntityTypeConfiguration<LossEvent>
{
    public void Configure(EntityTypeBuilder<LossEvent> builder)
    {
        builder.ToTable("LossEvents");
        builder.HasKey(lossEvent => lossEvent.Id);

        builder.Property(lossEvent => lossEvent.LossDate).IsRequired();
        builder.Property(lossEvent => lossEvent.LossDescription).IsRequired();
        builder.Property(lossEvent => lossEvent.LossLocation).HasMaxLength(500);
        builder.Property(lossEvent => lossEvent.CauseOfLossCode).HasMaxLength(50).IsRequired();
        builder.Property(lossEvent => lossEvent.ReportDate).IsRequired();
        builder.Property(lossEvent => lossEvent.PoliceReportNumber).HasMaxLength(100);

        // FRS §9.2 "FK to CauseOfLossCodes.Code". Codes are per organisation (D-12), so the key is
        // (OrganisationId, Code): a claim can only use a code of its own organisation (BR-C-05 backstop).
        builder.HasOne<CauseOfLossCodeEntity>()
            .WithMany()
            .HasForeignKey(ShadowColumns.OrganisationId, nameof(LossEvent.CauseOfLossCode))
            .HasPrincipalKey(ShadowColumns.OrganisationId, nameof(CauseOfLossCodeEntity.Code))
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex([nameof(LossEvent.LossDate)], "IX_LossEvents_LossDate");
    }
}
