using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class SearchProviderRefreshConfiguration : IEntityTypeConfiguration<SearchProviderRefresh>
{
    public void Configure(EntityTypeBuilder<SearchProviderRefresh> builder)
    {
        builder.ToTable("search_provider_refreshes");

        builder.HasKey(refresh => refresh.Id);

        builder.Property(refresh => refresh.Id)
            .ValueGeneratedNever();

        builder.Property(refresh => refresh.NormalizedQuery)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(refresh => refresh.ContentType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(refresh => refresh.Page)
            .IsRequired();

        builder.Property(refresh => refresh.LastRefreshedAtUtc)
            .IsRequired();

        builder.HasIndex(refresh => new { refresh.NormalizedQuery, refresh.ContentType, refresh.Page })
            .IsUnique();
    }
}
