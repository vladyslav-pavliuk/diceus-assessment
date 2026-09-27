using ClaimsModule.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>ClaimRiskObjects (FRS §9.4).</summary>
internal sealed class ClaimRiskObjectConfiguration : IEntityTypeConfiguration<ClaimRiskObject>
{
    public void Configure(EntityTypeBuilder<ClaimRiskObject> builder)
    {
        builder.ToTable("ClaimRiskObjects");
        builder.HasKey(riskObject => riskObject.Id);

        builder.Property(riskObject => riskObject.AssetType).IsRequired();
        builder.Property(riskObject => riskObject.AssetDescription).HasMaxLength(500).IsRequired();
        builder.Property(riskObject => riskObject.DamageDescription);
        builder.Property(riskObject => riskObject.IsPrimary).IsRequired().HasDefaultValue(false);
        builder.Property(riskObject => riskObject.AssetReference).HasMaxLength(255);
    }
}
