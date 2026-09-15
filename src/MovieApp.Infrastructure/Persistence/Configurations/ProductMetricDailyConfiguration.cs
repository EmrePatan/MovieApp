using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class ProductMetricDailyConfiguration : IEntityTypeConfiguration<ProductMetricDaily>
{
    public void Configure(EntityTypeBuilder<ProductMetricDaily> builder)
    {
        builder.ToTable("product_metric_daily");

        builder.HasKey(metric => new { metric.Date, metric.MetricName });

        builder.Property(metric => metric.MetricName)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(metric => metric.Count)
            .IsRequired();

        builder.Property(metric => metric.UpdatedAtUtc)
            .IsRequired();
    }
}
