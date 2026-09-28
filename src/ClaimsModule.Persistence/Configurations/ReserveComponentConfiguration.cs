using ClaimsModule.Domain.Reserves;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

internal sealed class ReserveComponentConfiguration : IEntityTypeConfiguration<ReserveComponent>
{
    public void Configure(EntityTypeBuilder<ReserveComponent> builder)
    {
        builder.ToTable("ClaimReserveComponents");
        builder.HasKey(component => component.Id);

        builder.Property(component => component.Component).IsRequired();
        builder.Property(component => component.CurrentAmount).IsRequired();
        builder.Property(component => component.LastChangeSequence).IsRequired();
        builder.Property(component => component.Status).IsRequired();
        builder.Property(component => component.Notes);

        // Protects LastChangeSequence and CurrentAmount (D-23).
        builder.Property<byte[]>(ShadowColumns.RowVer).IsRowVersion().IsRequired();

        builder.HasMany(component => component.Transactions)
            .WithOne()
            .HasForeignKey(transaction => transaction.ReserveComponentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex([nameof(ReserveComponent.ClaimId), nameof(ReserveComponent.Component)], "UX_ClaimReserveComponents_ClaimId_Component")
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
