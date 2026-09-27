using System.Text.Json;
using ClaimsModule.Domain.Organisations;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Persistence.Conventions;
using ClaimsModule.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>Policies (FRS §9.10), seeded with FRS §5.5 (+ D-34).</summary>
internal sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("Policies");
        builder.HasKey(policy => policy.Id);

        builder.Property(policy => policy.PolicyNumber).HasMaxLength(50).IsRequired();
        builder.Property(policy => policy.ClientName).HasMaxLength(255).IsRequired();
        builder.Property(policy => policy.EffectiveDate).IsRequired();   // DATE (FRS §9.10)
        builder.Property(policy => policy.ExpirationDate).IsRequired();  // DATE
        builder.Property(policy => policy.Status).IsRequired();

        // FRS §9.10 "Comma-separated list or JSON array": a JSON array in NVARCHAR(MAX) (D-33). The
        // comparer makes EF compare the list by content, so an unchanged list is not written back.
        builder.Property(policy => policy.CoverageTypes)
            .HasConversion(
                coverageTypes => CoverageTypesJson.Serialize(coverageTypes),
                json => CoverageTypesJson.Deserialize(json),
                new ValueComparer<IReadOnlyList<string>>(
                    (left, right) => left!.SequenceEqual(right!),
                    coverageTypes => coverageTypes.Aggregate(0, (hash, type) => HashCode.Combine(hash, type.GetHashCode(StringComparison.Ordinal))),
                    coverageTypes => coverageTypes.ToArray()))
            .IsRequired();

        builder.HasOne<Organisation>().WithMany().HasForeignKey(ShadowColumns.OrganisationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex([ShadowColumns.OrganisationId, nameof(Policy.PolicyNumber)], "UX_Policies_OrganisationId_PolicyNumber").IsUnique();
        builder.HasIndex([nameof(Policy.ClientName)], "IX_Policies_ClientName"); // policy search by client name (FRS §10.3)

        builder.HasData(SeedData.Policies.Select(policy => new
        {
            policy.Id,
            OrganisationId = SeedData.OrganisationId,
            policy.PolicyNumber,
            policy.ClientName,
            policy.EffectiveDate,
            policy.ExpirationDate,
            policy.Status,
            CoverageTypes = (IReadOnlyList<string>)policy.CoverageTypes,
            CreatedAt = SeedData.SeededAt,
            IsDeleted = false,
        }));
    }

    private static class CoverageTypesJson
    {
        public static string Serialize(IReadOnlyList<string> coverageTypes) => JsonSerializer.Serialize(coverageTypes);

        public static IReadOnlyList<string> Deserialize(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];
    }
}
