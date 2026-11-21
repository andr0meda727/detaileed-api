using Chat.Application.Abstractions;
using Chat.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Infrastructure.Persistence;
using Shared.Kernel.Tenancy;

namespace Chat.Infrastructure.Persistence;

public class ChatDbContext(DbContextOptions<ChatDbContext> options, ITenantContext tenantContext)
    : TenantDbContext(options, tenantContext), IChatDbContext
{
    public const string Schema = "chat";

    // Named in the configurations, so a lost race can be told apart from other failures
    public const string ConversationPairKey = "conversation_studio_id_customer_user_id_key";
    public const string MessageClientIdKey = "message_conversation_id_sender_user_id_client_message_id_key";

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public IQueryable<Conversation> ConversationsOfAnyStudio => Conversations.IgnoreQueryFilters([TenantFilter]);

    public IQueryable<Message> MessagesOfAnyStudio => Messages.IgnoreQueryFilters([TenantFilter]);

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);
    }

    public async Task<SaveOutcome> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            ChangeTracker.Clear();
            return SaveOutcome.Conflict;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
                                          {
                                              SqlState: PostgresErrorCodes.UniqueViolation,
                                              ConstraintName: ConversationPairKey or MessageClientIdKey
                                          })
        {
            ChangeTracker.Clear();
            return SaveOutcome.AlreadyExists;
        }
    }
}
