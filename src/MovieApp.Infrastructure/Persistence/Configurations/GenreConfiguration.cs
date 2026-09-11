using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.ToTable("genres");

        builder.HasKey(genre => genre.Id);

        builder.Property(genre => genre.Id)
            .ValueGeneratedNever();

        builder.Property(genre => genre.Name)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.NameMaxLength);

        builder.Property(genre => genre.CreatedAt)
            .IsRequired();

        builder.HasIndex(genre => genre.Name)
            .IsUnique();
    }
}
