using ClaimsModule.Domain.Organisations;
using ClaimsModule.Domain.Users;
using ClaimsModule.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username).HasMaxLength(255).IsRequired();
        builder.Property(user => user.DisplayName).HasMaxLength(255).IsRequired();
        builder.Property(user => user.Role).IsRequired();
        builder.Property(user => user.IsActive).IsRequired().HasDefaultValue(false);

        builder.HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(user => user.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique system-wide: sign-in resolves the user before the tenant is known.
        builder.HasIndex(user => user.Username).IsUnique();

        // Anonymous objects can set private setters and shadow properties (CreatedAt, IsDeleted).
        builder.HasData(SeedData.Users.Select(user => new
        {
            user.Id,
            OrganisationId = SeedData.OrganisationId,
            user.Username,
            user.DisplayName,
            user.Role,
            IsActive = true,
            CreatedAt = SeedData.SeededAt,
            IsDeleted = false,
        }));
    }
}
