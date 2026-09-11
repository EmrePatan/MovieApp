using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class SearchHistoryConfiguration : IEntityTypeConfiguration<SearchHistory>
{
    public void Configure(EntityTypeBuilder<SearchHistory> builder)
    {
        builder.ToTable("search_histories");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.Id)
            .ValueGeneratedNever();

        builder.Property(history => history.UserId)
            .IsRequired();

        builder.Property(history => history.Query)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(history => history.NormalizedQuery)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(history => history.SearchedAt)
            .IsRequired();

        builder.HasOne(history => history.User)
            .WithMany(user => user.SearchHistories)
            .HasForeignKey(history => history.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(history => new { history.UserId, history.SearchedAt });

        builder.HasIndex(history => new { history.UserId, history.NormalizedQuery });
    }
}
