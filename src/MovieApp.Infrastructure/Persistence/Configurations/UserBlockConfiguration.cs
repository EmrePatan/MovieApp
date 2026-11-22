using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.ToTable("user_blocks", table =>
            table.HasCheckConstraint("CK_user_blocks_distinct_users", "\"BlockerId\" <> \"BlockedUserId\""));

        builder.HasKey(block => block.Id);
        builder.Property(block => block.Id).ValueGeneratedNever();
        builder.Property(block => block.BlockerId).IsRequired();
        builder.Property(block => block.BlockedUserId).IsRequired();
        builder.Property(block => block.CreatedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(block => block.BlockerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(block => block.BlockedUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(block => new { block.BlockerId, block.BlockedUserId }).IsUnique();
        builder.HasIndex(block => block.BlockedUserId);
    }
}
