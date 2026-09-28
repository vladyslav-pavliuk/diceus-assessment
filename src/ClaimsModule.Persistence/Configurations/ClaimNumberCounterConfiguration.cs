using ClaimsModule.Domain.Organisations;
using ClaimsModule.Persistence.ClaimNumbers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>No seed: the first claim of a year inserts its row (D-10).</summary>
internal sealed class ClaimNumberCounterConfiguration : IEntityTypeConfiguration<ClaimNumberCounter>
{
    public void Configure(EntityTypeBuilder<ClaimNumberCounter> builder)
    {
        builder.ToTable("ClaimNumberCounters", table => table.HasCheckConstraint("CK_ClaimNumberCounters_LastValue", "[LastValue] BETWEEN 1 AND 9999999"));
        builder.HasKey(counter => new { counter.OrganisationId, counter.Year });

        builder.Property(counter => counter.Year).ValueGeneratedNever();
        builder.Property(counter => counter.LastValue).IsRequired();

        builder.HasOne<Organisation>().WithMany().HasForeignKey(counter => counter.OrganisationId).OnDelete(DeleteBehavior.Restrict);
    }
}
