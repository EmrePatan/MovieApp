using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class SocialReportConfiguration : IEntityTypeConfiguration<SocialReport>
{
    public void Configure(EntityTypeBuilder<SocialReport> builder)
    {
        builder.ToTable("social_reports");
        builder.HasKey(report => report.Id);
        builder.Property(report => report.Id).ValueGeneratedNever();
        builder.Property(report => report.ReporterId).IsRequired();
        builder.Property(report => report.TargetType).HasMaxLength(24).IsRequired();
        builder.Property(report => report.TargetId).IsRequired();
        builder.Property(report => report.Reason).HasMaxLength(64).IsRequired();
        builder.Property(report => report.Details).HasMaxLength(1000);
        builder.Property(report => report.Status).HasMaxLength(16).IsRequired();
        builder.Property(report => report.CreatedAt).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(report => report.ReporterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(report => new { report.ReporterId, report.CreatedAt });
        builder.HasIndex(report => new { report.TargetType, report.TargetId, report.CreatedAt });
    }
}
