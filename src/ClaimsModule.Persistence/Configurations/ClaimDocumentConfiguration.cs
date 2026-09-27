using ClaimsModule.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

/// <summary>ClaimDocuments (FRS §9.7): metadata only; the bytes are in blob storage.</summary>
internal sealed class ClaimDocumentConfiguration : IEntityTypeConfiguration<ClaimDocument>
{
    public void Configure(EntityTypeBuilder<ClaimDocument> builder)
    {
        builder.ToTable("ClaimDocuments");
        builder.HasKey(document => document.Id);

        builder.Property(document => document.DocumentType).HasMaxLength(100).IsRequired(); // FRS §9.7: NVARCHAR(100)
        builder.Property(document => document.DocumentName).HasMaxLength(255).IsRequired();
        builder.Property(document => document.BlobPath).HasMaxLength(500).IsRequired();
        builder.Property(document => document.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(document => document.FileSizeBytes).IsRequired();
        builder.Property(document => document.UploadedAt).IsRequired();
        builder.Property(document => document.Notes).HasMaxLength(500);
    }
}
