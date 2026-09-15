using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class KeywordConfiguration : IEntityTypeConfiguration<Keyword>
{
    public void Configure(EntityTypeBuilder<Keyword> builder)
    {
        builder.ToTable("keywords");

        builder.HasKey(keyword => keyword.Id);

        builder.Property(keyword => keyword.Id)
            .ValueGeneratedNever();

        builder.Property(keyword => keyword.TmdbKeywordId)
            .IsRequired();

        builder.Property(keyword => keyword.Name)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.NameMaxLength);

        builder.Property(keyword => keyword.CreatedAt)
            .IsRequired();

        builder.Property(keyword => keyword.UpdatedAt)
            .IsRequired();

        builder.HasIndex(keyword => keyword.TmdbKeywordId)
            .IsUnique();
    }
}
