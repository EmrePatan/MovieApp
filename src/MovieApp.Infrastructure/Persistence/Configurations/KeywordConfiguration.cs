using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class KeywordConfiguration : IEntityTypeConfiguration<Keyword>
{
    public void Configure(EntityTypeBuilder<Keyword> builder)
    {
        builder.ToTable("keywords");

        builder.HasKey(keyword => keyword.Id);

        builder.Property(keyword => keyword.Id)
            .ValueGeneratedNever();

        builder.Property(keyword => keyword.Name)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.NameMaxLength);

        builder.Property(keyword => keyword.CanonicalName)
            .HasMaxLength(ConfigurationConstants.NameMaxLength);

        builder.Property(keyword => keyword.NormalizedName)
            .HasMaxLength(ConfigurationConstants.NameMaxLength);

        builder.Property(keyword => keyword.SemanticCategory)
            .IsRequired()
            .HasDefaultValue(KeywordSemanticCategory.Unknown);

        builder.Property(keyword => keyword.ClassificationStatus)
            .IsRequired()
            .HasDefaultValue(KeywordClassificationStatus.Auto);

        builder.Property(keyword => keyword.CreatedAt)
            .IsRequired();

        builder.Property(keyword => keyword.UpdatedAt)
            .IsRequired();

        builder.HasIndex(keyword => keyword.TmdbKeywordId)
            .IsUnique();

        builder.HasIndex(keyword => keyword.NormalizedName);
    }
}
