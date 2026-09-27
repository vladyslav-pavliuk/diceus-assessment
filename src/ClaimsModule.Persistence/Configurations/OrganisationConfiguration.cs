using ClaimsModule.Domain.Organisations;
using ClaimsModule.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

internal sealed class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable("Organisations");
        builder.HasKey(organisation => organisation.Id);

        builder.Property(organisation => organisation.Name).HasMaxLength(255).IsRequired();

        // Anonymous objects can set private setters and shadow properties (CreatedAt, IsDeleted).
        builder.HasData(new
        {
            Id = SeedData.OrganisationId,
            Name = SeedData.OrganisationName,
            CreatedAt = SeedData.SeededAt,
            IsDeleted = false,
        });
    }
}
