namespace Chat.Application.Abstractions;

/// <summary>
/// The module's unit of work. Conversations and messages belong to a studio and are tenant-filtered; the customer
/// side reads them across studios only through explicitly named sets.
/// </summary>
public interface IChatDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
