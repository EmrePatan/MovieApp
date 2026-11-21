using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Configurations;

internal sealed class DirectConversationConfiguration : IEntityTypeConfiguration<DirectConversation>
{
    public void Configure(EntityTypeBuilder<DirectConversation> builder)
    {
        builder.ToTable("direct_conversations");
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Id).ValueGeneratedNever();
        builder.Property(conversation => conversation.CreatedAt).IsRequired();
        builder.Property(conversation => conversation.UpdatedAt).IsRequired();
        builder.HasIndex(conversation => conversation.LastMessageAt);
    }
}
