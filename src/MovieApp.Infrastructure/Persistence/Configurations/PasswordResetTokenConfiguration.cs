using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .ValueGeneratedNever();

        builder.Property(token => token.UserId)
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(token => token.CreatedAtUtc)
            .IsRequired();

        builder.Property(token => token.ExpiresAtUtc)
            .IsRequired();

        builder.Property(token => token.ProtectedDeliverySecret)
            .HasMaxLength(512);

        builder.Property(token => token.ContentLocale)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("en-US");

        builder.Property(token => token.DeliveryCompletedAtUtc);

        builder.HasOne(token => token.User)
            .WithMany(user => user.PasswordResetTokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        builder.HasIndex(token => new { token.UserId, token.ExpiresAtUtc });
    }
}
