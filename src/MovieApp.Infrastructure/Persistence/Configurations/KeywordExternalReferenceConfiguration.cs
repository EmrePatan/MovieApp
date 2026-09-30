using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class KeywordExternalReferenceConfiguration : IEntityTypeConfiguration<KeywordExternalReference>
{
    public void Configure(EntityTypeBuilder<KeywordExternalReference> builder)
    {
        builder.ToTable("keyword_external_references");

        builder.HasKey(reference => new { reference.Provider, reference.ExternalId });

        builder.Property(reference => reference.Provider)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(reference => reference.ExternalId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(reference => reference.ExternalName)
            .HasMaxLength(ConfigurationConstants.NameMaxLength);

        builder.Property(reference => reference.CreatedAt)
            .IsRequired();

        builder.HasOne(reference => reference.Keyword)
            .WithMany(keyword => keyword.ExternalReferences)
            .HasForeignKey(reference => reference.KeywordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(reference => reference.KeywordId);
        builder.HasIndex(reference => new { reference.Provider, reference.KeywordId });
    }
}
