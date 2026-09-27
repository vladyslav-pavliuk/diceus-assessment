using ClaimsModule.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>ClaimParties (FRS §9.3).</summary>
internal sealed class ClaimPartyConfiguration : IEntityTypeConfiguration<ClaimParty>
{
    public void Configure(EntityTypeBuilder<ClaimParty> builder)
    {
        builder.ToTable("ClaimParties");
        builder.HasKey(party => party.Id);

        builder.Property(party => party.PartyRole).IsRequired();
        builder.Property(party => party.PartyType).HasMaxLength(20).IsRequired(); // FRS §9.3: NVARCHAR(20)
        builder.Property(party => party.FirstName).HasMaxLength(100);
        builder.Property(party => party.LastName).HasMaxLength(100);
        builder.Property(party => party.CompanyName).HasMaxLength(255);
        builder.Property(party => party.Email).HasMaxLength(255);
        builder.Property(party => party.Phone).HasMaxLength(50);
        builder.Property(party => party.Notes);
        builder.Property(party => party.IsActive).IsRequired().HasDefaultValue(false);

        // "At least one active Claimant" (BR-C-03, CC-03, PTY-01).
        builder.HasIndex([nameof(ClaimParty.ClaimId), nameof(ClaimParty.PartyRole), nameof(ClaimParty.IsActive)], "IX_ClaimParties_ClaimId_PartyRole_IsActive");
    }
}
