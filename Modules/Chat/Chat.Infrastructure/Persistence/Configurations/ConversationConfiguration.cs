using Chat.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat.Infrastructure.Persistence.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversation", ChatDbContext.Schema);

        builder.HasKey(e => e.Id).HasName("conversation_pkey");
        builder.Property(e => e.Id).ValueGeneratedNever().HasColumnName("id");

        // One thread per studio and customer
        builder.HasIndex(e => new { e.StudioId, e.CustomerUserId }, ChatDbContext.ConversationPairKey).IsUnique();

        // A studio's inbox and a customer's conversations, latest first
        builder.HasIndex(e => new { e.StudioId, e.LastMessageAt, e.Id }, "IX_conversation_studio_id_last_message_at_id")
            .IsDescending(false, true, true);
        builder.HasIndex(e => new { e.CustomerUserId, e.LastMessageAt, e.Id }, "IX_conversation_customer_user_id_last_message_at_id")
            .IsDescending(false, true, true);

        builder.Property(e => e.StudioId).HasColumnName("studio_id");
        builder.Property(e => e.CustomerUserId).HasColumnName("customer_user_id");
        builder.Property(e => e.StudioName).HasMaxLength(100).HasColumnName("studio_name");
        builder.Property(e => e.StudioNameAsOf).HasColumnName("studio_name_as_of");
        builder.Property(e => e.CustomerName).HasMaxLength(200).HasColumnName("customer_name");
        builder.Property(e => e.LastMessageAt).HasColumnName("last_message_at");
        builder.Property(e => e.LastMessagePreview).HasMaxLength(Conversation.PreviewLength).HasColumnName("last_message_preview");
        builder.Property(e => e.CustomerReadUpTo).HasColumnName("customer_read_up_to");
        builder.Property(e => e.StudioReadUpTo).HasColumnName("studio_read_up_to");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");

        // Mapped by Npgsql to Postgres' xmin system column (no column is added): a message and a read receipt, or
        // two messages, changing the same conversation at once can't silently undo each other
        builder.Property<uint>("Version").IsRowVersion();
    }
}
