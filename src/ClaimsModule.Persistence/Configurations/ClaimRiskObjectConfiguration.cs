using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

internal sealed class ClaimRiskObjectConfiguration : IEntityTypeConfiguration<ClaimRiskObject>
{
    public void Configure(EntityTypeBuilder<ClaimRiskObject> builder)
    {
        builder.ToTable("ClaimRiskObjects");
        builder.HasKey(riskObject => riskObject.Id);

        builder.Property(riskObject => riskObject.AssetType).IsRequired();
        builder.Property(riskObject => riskObject.AssetDescription).HasMaxLength(FieldLengths.AssetDescription).IsRequired();
        builder.Property(riskObject => riskObject.DamageDescription);
        builder.Property(riskObject => riskObject.IsPrimary).IsRequired().HasDefaultValue(false);
        builder.Property(riskObject => riskObject.AssetReference).HasMaxLength(FieldLengths.AssetReference);
    }
}
