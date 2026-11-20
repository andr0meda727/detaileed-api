using Marketplace.Application.Abstractions;
using Marketplace.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Marketplace.Application.Listings.PublishListing;

public sealed record PublishListingCommand(bool Publish) : ICommand<Result>;

public sealed class PublishListingHandler(IMarketplaceDbContext db, ITenantContext tenant)
    : ICommandHandler<PublishListingCommand, Result>
{
    /// <summary>
    /// Shows the studio on the customer map, or hides it again.
    /// </summary>
    public async ValueTask<Result> Handle(PublishListingCommand command, CancellationToken cancellationToken)
    {
        var publish = command.Publish;
        var studioId = tenant.StudioId ?? throw new InvalidOperationException("This needs a resolved studio.");

        var listing = await db.Listings.FirstOrDefaultAsync(l => l.StudioId == studioId, cancellationToken);
        if (listing == null) return ListingErrors.NotFound;

        if (publish)
        {
            var result = listing.Publish();
            if (!result.Success) return result;
        }
        else
        {
            listing.Unpublish();
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
