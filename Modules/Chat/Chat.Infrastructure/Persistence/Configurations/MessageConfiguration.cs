using Chat.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat.Infrastructure.Persistence.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("message", ChatDbContext.Schema);

        builder.HasKey(e => e.Id).HasName("message_pkey");
        builder.Property(e => e.Id).ValueGeneratedNever().HasColumnName("id");

        builder.HasOne<Conversation>().WithMany()
            .HasForeignKey(e => e.ConversationId)
            .HasConstraintName("message_conversation_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);

        // History in order, read with a (sent_at, id) cursor
        builder.HasIndex(e => new { e.ConversationId, e.SentAt, e.Id }, "IX_message_conversation_id_sent_at_id");

        // A retried send finds the message it already saved
        builder.HasIndex(e => new { e.ConversationId, e.SenderUserId, e.ClientMessageId }, ChatDbContext.MessageClientIdKey)
            .IsUnique();

        builder.Property(e => e.ConversationId).HasColumnName("conversation_id");
        builder.Property(e => e.StudioId).HasColumnName("studio_id");
        builder.Property(e => e.Side).HasConversion<string>().HasMaxLength(20).HasColumnName("side");
        builder.Property(e => e.SenderUserId).HasColumnName("sender_user_id");
        builder.Property(e => e.Body).HasMaxLength(Conversation.MaxBodyLength).HasColumnName("body");
        builder.Property(e => e.SentAt).HasColumnName("sent_at");
        builder.Property(e => e.ClientMessageId).HasColumnName("client_message_id");
    }
}
