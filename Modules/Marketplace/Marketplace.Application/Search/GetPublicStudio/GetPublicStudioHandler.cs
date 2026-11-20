using Identity.Contracts;
using Marketplace.Application.Abstractions;
using Marketplace.Application.Listings;
using Marketplace.Application.Reviews;
using Marketplace.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Results;

namespace Marketplace.Application.Search.GetPublicStudio;

public sealed record GetPublicStudioQuery(Guid StudioId) : IQuery<Result<ListingDto>>;

public sealed class GetPublicStudioHandler(IMarketplaceDbContext db, IStudioDirectory studios)
    : IQueryHandler<GetPublicStudioQuery, Result<ListingDto>>
{
    /// <summary>
    /// A studio's public page. An unpublished studio is reported as missing, the same as one that doesn't exist.
    /// </summary>
    public async ValueTask<Result<ListingDto>> Handle(GetPublicStudioQuery query, CancellationToken cancellationToken)
    {
        var studioId = query.StudioId;
        var listing = await db.Listings.AsNoTracking()
            .FirstOrDefaultAsync(l => l.StudioId == studioId && l.IsPublished, cancellationToken);

        if (listing == null) return ListingErrors.NotFound;

        return ListingDto.From(
            listing, await studios.OfAsync(studioId, cancellationToken), await db.RatingAsync(studioId, cancellationToken));
    }
}
