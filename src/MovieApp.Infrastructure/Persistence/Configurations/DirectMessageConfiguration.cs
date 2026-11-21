using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class DirectMessageConfiguration : IEntityTypeConfiguration<DirectMessage>
{
    public void Configure(EntityTypeBuilder<DirectMessage> builder)
    {
        builder.ToTable("direct_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.ConversationId).IsRequired();
        builder.Property(message => message.SenderId).IsRequired();
        builder.Property(message => message.Body).HasMaxLength(DirectMessage.MaxBodyLength).IsRequired();
        builder.Property(message => message.CreatedAt).IsRequired();
        builder.HasOne<DirectConversation>()
            .WithMany()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(message => message.SenderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(message => new { message.ConversationId, message.CreatedAt, message.Id });
        builder.HasIndex(message => message.SenderId);
    }
}
