using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class DirectConversationParticipantConfiguration : IEntityTypeConfiguration<DirectConversationParticipant>
{
    public void Configure(EntityTypeBuilder<DirectConversationParticipant> builder)
    {
        builder.ToTable("direct_conversation_participants");
        builder.HasKey(participant => participant.Id);
        builder.Property(participant => participant.Id).ValueGeneratedNever();
        builder.Property(participant => participant.ConversationId).IsRequired();
        builder.Property(participant => participant.UserId).IsRequired();
        builder.HasOne<DirectConversation>()
            .WithMany()
            .HasForeignKey(participant => participant.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(participant => participant.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(participant => new { participant.ConversationId, participant.UserId }).IsUnique();
        builder.HasIndex(participant => participant.UserId);
    }
}
