using ClaimsModule.Domain.Claims;
using ClaimsModule.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>
/// ClaimStatusTransitions (FRS §4.2, D-09): global configuration, so no OrganisationId (D-12). Seeded
/// from <see cref="ClaimStatusTransition.FrsDefaults"/>, the single declaration of the table.
/// </summary>
internal sealed class ClaimStatusTransitionConfiguration : IEntityTypeConfiguration<ClaimStatusTransition>
{
    public void Configure(EntityTypeBuilder<ClaimStatusTransition> builder)
    {
        builder.ToTable("ClaimStatusTransitions");
        builder.HasKey(transition => transition.Id);

        builder.Property(transition => transition.FromStatus).IsRequired();
        builder.Property(transition => transition.ToStatus).IsRequired();
        builder.Property(transition => transition.MinimumRole);
        builder.Property(transition => transition.RequiresReason).IsRequired().HasDefaultValue(false);
        builder.Property(transition => transition.IsSystemOnly).IsRequired().HasDefaultValue(false);

        builder.HasIndex([nameof(ClaimStatusTransition.FromStatus), nameof(ClaimStatusTransition.ToStatus)], "UX_ClaimStatusTransitions_FromStatus_ToStatus")
            .IsUnique();

        builder.HasData(ClaimStatusTransition.FrsDefaults().Select(transition => new
        {
            Id = SeedData.TransitionIds[(transition.FromStatus, transition.ToStatus)],
            transition.FromStatus,
            transition.ToStatus,
            transition.MinimumRole,
            transition.RequiresReason,
            transition.IsSystemOnly,
            CreatedAt = SeedData.SeededAt,
            IsDeleted = false,
        }));
    }
}
