using Identity.Contracts;
using Marketplace.Application.Abstractions;
using Marketplace.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Scheduling.Contracts;
using Shared.Kernel.Tenancy;

namespace Marketplace.Application.Listings.SaveListing;

public sealed record SaveListingCommand(ListingRequest Request) : ICommand<ListingDto>;

public sealed class SaveListingHandler(
    IMarketplaceDbContext db, ITenantContext tenant, IServiceCatalogSource catalogSource, IStudioDirectory studios)
    : ICommandHandler<SaveListingCommand, ListingDto>
{
    /// <summary>
    /// Creates the studio's listing or updates its profile. A new listing starts unpublished, with the studio's
    /// current services copied from Scheduling; later changes arrive as events.
    /// </summary>
    public async ValueTask<ListingDto> Handle(SaveListingCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var studioId = tenant.StudioId ?? throw new InvalidOperationException("This needs a resolved studio.");

        var listing = await db.Listings.FirstOrDefaultAsync(l => l.StudioId == studioId, cancellationToken);
        if (listing == null)
        {
            listing = StudioListing.Create(studioId, request.ToProfile());
            CatalogCopy.ApplyTo(listing, await catalogSource.GetCatalogAsync(studioId, cancellationToken));
            db.Listings.Add(listing);
        }
        else
        {
            listing.UpdateProfile(request.ToProfile());
        }

        await db.SaveChangesAsync(cancellationToken);
        return ListingDto.From(listing, await studios.OfAsync(studioId, cancellationToken));
    }
}
