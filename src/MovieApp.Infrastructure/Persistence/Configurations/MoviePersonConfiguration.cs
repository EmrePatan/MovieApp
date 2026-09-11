using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class MoviePersonConfiguration : IEntityTypeConfiguration<MoviePerson>
{
    public void Configure(EntityTypeBuilder<MoviePerson> builder)
    {
        builder.ToTable("movie_people");

        builder.HasKey(moviePerson => new
        {
            moviePerson.MovieId,
            moviePerson.PersonId,
            moviePerson.CreditType,
            moviePerson.Job
        });

        builder.Property(moviePerson => moviePerson.Job)
            .HasMaxLength(ConfigurationConstants.CreditFieldMaxLength)
            .HasDefaultValue(string.Empty);

        builder.Property(moviePerson => moviePerson.Character)
            .HasMaxLength(ConfigurationConstants.CreditFieldMaxLength);

        builder.Property(moviePerson => moviePerson.Department)
            .HasMaxLength(ConfigurationConstants.CreditFieldMaxLength);

        builder.Property(moviePerson => moviePerson.CreditType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasOne(moviePerson => moviePerson.Movie)
            .WithMany(movie => movie.MoviePeople)
            .HasForeignKey(moviePerson => moviePerson.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(moviePerson => moviePerson.Person)
            .WithMany(person => person.MoviePeople)
            .HasForeignKey(moviePerson => moviePerson.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(moviePerson => moviePerson.PersonId);
    }
}
