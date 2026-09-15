using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class UserExternalLoginConfiguration : IEntityTypeConfiguration<UserExternalLogin>
{
    public void Configure(EntityTypeBuilder<UserExternalLogin> builder)
    {
        builder.ToTable("user_external_logins");

        builder.HasKey(login => login.Id);

        builder.Property(login => login.Id)
            .ValueGeneratedNever();

        builder.Property(login => login.UserId)
            .IsRequired();

        builder.Property(login => login.Provider)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(login => login.ProviderSubject)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(login => login.EmailAtLinkTime)
            .HasMaxLength(320);

        builder.Property(login => login.CreatedAt)
            .IsRequired();

        builder.HasIndex(login => new { login.Provider, login.ProviderSubject })
            .IsUnique();

        builder.HasOne(login => login.User)
            .WithMany(user => user.ExternalLogins)
            .HasForeignKey(login => login.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
