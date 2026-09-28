using ClaimsModule.Domain.Organisations;
using ClaimsModule.Domain.ReferenceData;
using ClaimsModule.Persistence.Conventions;
using ClaimsModule.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

internal sealed class CauseOfLossCodeConfiguration : IEntityTypeConfiguration<CauseOfLossCode>
{
    public void Configure(EntityTypeBuilder<CauseOfLossCode> builder)
    {
        builder.ToTable("CauseOfLossCodes");
        builder.HasKey(code => code.Id);

        builder.Property(code => code.Code).HasMaxLength(50).IsRequired();
        builder.Property(code => code.Name).HasMaxLength(255).IsRequired();
        builder.Property(code => code.PerilCategory).IsRequired();
        builder.Property(code => code.IsActive).IsRequired().HasDefaultValue(false);
        builder.Property(code => code.SortOrder).IsRequired();
        builder.Property(code => code.Notes).HasMaxLength(500);

        builder.HasOne<Organisation>().WithMany().HasForeignKey(ShadowColumns.OrganisationId).OnDelete(DeleteBehavior.Restrict);

        // Unique per organisation (D-12); an alternate key, because LossEvents references it.
        builder.HasAlternateKey(ShadowColumns.OrganisationId, nameof(CauseOfLossCode.Code)).HasName("AK_CauseOfLossCodes_OrganisationId_Code");

        builder.HasData(SeedData.CauseOfLossCodes.Select((code, index) => new
        {
            code.Id,
            OrganisationId = SeedData.OrganisationId,
            code.Code,
            code.Name,
            code.PerilCategory,
            IsActive = true,
            SortOrder = (index + 1) * 10,
            code.Notes,
            CreatedAt = SeedData.SeededAt,
            IsDeleted = false,
        }));
    }
}
