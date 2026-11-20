using Identity.Contracts;
using Marketplace.Application.Abstractions;
using Marketplace.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;
using Shared.Kernel.Tenancy;

namespace Marketplace.Application.Listings.GetMyListing;

public sealed record GetMyListingQuery : IQuery<Result<ListingDto>>;

public sealed class GetMyListingHandler(IMarketplaceDbContext db, ITenantContext tenant, IStudioDirectory studios)
    : IQueryHandler<GetMyListingQuery, Result<ListingDto>>
{
    public async ValueTask<Result<ListingDto>> Handle(GetMyListingQuery query, CancellationToken cancellationToken)
    {
        var studioId = tenant.StudioId ?? throw new InvalidOperationException("This needs a resolved studio.");

        // Listings aren't tenant-filtered, so the caller's studio is matched here
        var listing = await db.Listings.AsNoTracking().FirstOrDefaultAsync(l => l.StudioId == studioId, cancellationToken);

        if (listing == null) return ListingErrors.NotFound;

        return ListingDto.From(
            listing, await studios.OfAsync(studioId, cancellationToken),
            await Reviews.RatingQueries.RatingAsync(db, studioId, cancellationToken));
    }
}
