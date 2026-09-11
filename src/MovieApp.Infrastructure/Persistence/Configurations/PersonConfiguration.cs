using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("people");

        builder.HasKey(person => person.Id);

        builder.Property(person => person.Id)
            .ValueGeneratedNever();

        builder.Property(person => person.Name)
            .IsRequired()
            .HasMaxLength(ConfigurationConstants.NameMaxLength);

        builder.Property(person => person.ProfilePath)
            .HasMaxLength(ConfigurationConstants.PathMaxLength);

        builder.Property(person => person.ImdbId)
            .HasMaxLength(ConfigurationConstants.ImdbIdMaxLength);

        builder.Property(person => person.CreatedAt)
            .IsRequired();

        builder.Property(person => person.UpdatedAt)
            .IsRequired();

        builder.HasIndex(person => person.TmdbId).AsUniqueExternalIdIndex("TmdbId");
        builder.HasIndex(person => person.TvdbId).AsUniqueExternalIdIndex("TvdbId");
        builder.HasIndex(person => person.ImdbId).AsUniqueExternalIdIndex("ImdbId");

        builder.HasIndex(person => person.Name);
    }
}
