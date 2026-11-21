using FluentValidation;
using Marketplace.Domain;
using Scheduling.Contracts;
using Shared.Kernel.Vehicles;

namespace Marketplace.Application.Listings;

/// <summary>
/// What the owner writes on the listing itself. The name, phone, address and pin are the studio's details, edited
/// in the studio settings (PUT /api/studios/current) and copied here.
/// </summary>
public sealed record ListingRequest(string? Description);

public sealed record ListingPriceDto(VehicleSize VehicleSize, decimal Price);

public sealed record ListingServiceDto(Guid Id, string Name, ServiceCategory Category, List<ListingPriceDto> Prices);

/// <summary>
/// A listing with its services and prices, as both the owner and customers see it.
/// <see cref="TimeZone"/> is the studio's IANA zone (from Identity), in which its opening hours and slots are meant.
/// The address, city and pin are null until the owner fills them in the studio settings
/// (<see cref="HasCompleteDetails"/>); a published listing always has them.
/// </summary>
public sealed record ListingDto(
    Guid StudioId, string Name, string? Description, string? Address, string? City, string? Phone,
    double? Latitude, double? Longitude, bool IsPublished, bool HasCompleteDetails, string TimeZone,
    List<ServiceCategory> Categories, List<ListingServiceDto> Services,
    double? AverageRating = null, int ReviewCount = 0)
{
    public static ListingDto From(StudioListing listing, string timeZone, Reviews.RatingSummary? rating = null) => new(
        listing.StudioId, listing.Name, listing.Description, listing.Address, listing.City, listing.Phone,
        listing.Latitude, listing.Longitude, listing.IsPublished, listing.HasCompleteDetails, timeZone,
        listing.Categories.ToList(),
        listing.Services
            .OrderBy(s => s.Category).ThenBy(s => s.Name)
            .Select(s => new ListingServiceDto(s.ServiceId, s.Name, s.Category,
                listing.Prices.Where(p => p.ServiceId == s.ServiceId).OrderBy(p => p.Size)
                    .Select(p => new ListingPriceDto(p.Size, p.AmountInCents / 100m)).ToList()))
            .ToList(),
        rating?.AverageRating, rating?.ReviewCount ?? 0);
}

public class ListingRequestValidator : AbstractValidator<ListingRequest>
{
    public ListingRequestValidator()
    {
        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Opis nie może przekraczać 2000 znaków.");
    }
}
