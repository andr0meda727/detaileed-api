using Identity.Contracts;
using Marketplace.Application.Listings;
using Marketplace.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Consumers;

/// <summary>
/// Keeps each listing's copy of the studio's name, phone and location in step with Identity. A studio without a
/// listing is skipped: its details are copied when the owner creates one. Redelivered or out-of-order snapshots are
/// harmless, because an older one never replaces a newer one, and concurrent updates are caught by the listing's row
/// version and retried by the bus. The save empties the public cache, so customers see the change at once.
/// </summary>
public class ListingStudioDetailsConsumer(MarketplaceDbContext db) : IConsumer<StudioDetailsChanged>
{
    public async Task Consume(ConsumeContext<StudioDetailsChanged> context)
    {
        var message = context.Message;

        var listing = await db.Listings.FirstOrDefaultAsync(l => l.StudioId == message.StudioId, context.CancellationToken);
        if (listing == null) return;

        if (!listing.ApplyStudioDetails(message.Details.ToListed(), message.ChangedAt)) return;
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
