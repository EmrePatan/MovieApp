using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class TvShowFollowConfiguration : IEntityTypeConfiguration<TvShowFollow>
{
    public void Configure(EntityTypeBuilder<TvShowFollow> builder)
    {
        builder.ToTable("tv_show_follows");

        builder.HasKey(follow => follow.Id);

        builder.Property(follow => follow.Id)
            .ValueGeneratedNever();

        builder.Property(follow => follow.UserId)
            .IsRequired();

        builder.Property(follow => follow.TvShowId)
            .IsRequired();

        builder.Property(follow => follow.NotifyNewSeasons)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(follow => follow.NotifyNewEpisodes)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(follow => follow.CreatedAt)
            .IsRequired();

        builder.Property(follow => follow.UpdatedAt)
            .IsRequired();

        builder.HasOne(follow => follow.User)
            .WithMany(user => user.TvShowFollows)
            .HasForeignKey(follow => follow.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(follow => follow.TvShow)
            .WithMany(tvShow => tvShow.TvShowFollows)
            .HasForeignKey(follow => follow.TvShowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(follow => follow.UserId);

        builder.HasIndex(follow => follow.TvShowId);

        builder.HasIndex(follow => new { follow.UserId, follow.TvShowId })
            .IsUnique();
    }
}
