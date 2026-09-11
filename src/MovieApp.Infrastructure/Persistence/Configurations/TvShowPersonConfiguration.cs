using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class TvShowPersonConfiguration : IEntityTypeConfiguration<TvShowPerson>
{
    public void Configure(EntityTypeBuilder<TvShowPerson> builder)
    {
        builder.ToTable("tv_show_people");

        builder.HasKey(tvShowPerson => new
        {
            tvShowPerson.TvShowId,
            tvShowPerson.PersonId,
            tvShowPerson.CreditType,
            tvShowPerson.Job
        });

        builder.Property(tvShowPerson => tvShowPerson.Job)
            .HasMaxLength(ConfigurationConstants.CreditFieldMaxLength)
            .HasDefaultValue(string.Empty);

        builder.Property(tvShowPerson => tvShowPerson.Character)
            .HasMaxLength(ConfigurationConstants.CreditFieldMaxLength);

        builder.Property(tvShowPerson => tvShowPerson.Department)
            .HasMaxLength(ConfigurationConstants.CreditFieldMaxLength);

        builder.Property(tvShowPerson => tvShowPerson.CreditType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasOne(tvShowPerson => tvShowPerson.TvShow)
            .WithMany(tvShow => tvShow.TvShowPeople)
            .HasForeignKey(tvShowPerson => tvShowPerson.TvShowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tvShowPerson => tvShowPerson.Person)
            .WithMany(person => person.TvShowPeople)
            .HasForeignKey(tvShowPerson => tvShowPerson.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(tvShowPerson => tvShowPerson.PersonId);
    }
}
