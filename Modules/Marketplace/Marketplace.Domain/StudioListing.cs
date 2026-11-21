using Scheduling.Contracts;
using Shared.Kernel.Auditing;
using Shared.Kernel.Results;
using Shared.Kernel.Vehicles;

namespace Marketplace.Domain;

/// <summary>
/// A studio's public profile on the customer map. The owner writes only the description and decides when it is
/// published; the name, phone and location are a copy of the studio's details (kept in step with Identity) and the
/// services and prices a copy of its catalogue (kept in step with Scheduling). It is public data, so it is not
/// tenant-scoped; there is one per studio, keyed by the studio's ID. Customers only see it once it is published.
/// </summary>
public class StudioListing : IAuditableEntity
{
    private readonly List<ListedService> _services = [];
    private readonly List<ListedPrice> _prices = [];

    // For EF Core
    private StudioListing() { }

    public Guid StudioId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public string? Address { get; private set; }

    public string? City { get; private set; }

    public string? Phone { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public bool IsPublished { get; private set; }

    // When Scheduling took the copied catalogue; older snapshots are ignored
    public DateTime? CatalogChangedAt { get; private set; }

    // When Identity took the copied studio details; older snapshots are ignored
    public DateTime? StudioDetailsChangedAt { get; private set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public IReadOnlyCollection<ListedService> Services => _services;

    public IReadOnlyCollection<ListedPrice> Prices => _prices;

    /// <summary>
    /// Whether customers could find the studio: it has an address, a city and a pin on the map.
    /// </summary>
    public bool HasCompleteDetails => Address != null && City != null && Latitude != null && Longitude != null;

    public static StudioListing Create(Guid studioId, ListedStudioDetails details, DateTime detailsChangedAt)
    {
        if (studioId == Guid.Empty) throw new ArgumentException("A listing belongs to a studio.", nameof(studioId));

        var listing = new StudioListing { StudioId = studioId };
        listing.ApplyStudioDetails(details, detailsChangedAt);
        return listing;
    }

    public void UpdateDescription(string? description) => Description = Blank(description);

    /// <summary>
    /// Replaces the copied studio details with a newer snapshot. Returns false, changing nothing, for a snapshot
    /// older than the one already applied, since messages can arrive out of order. A published listing whose details
    /// are no longer complete is hidden again, so the map never shows a studio it can't place.
    /// </summary>
    public bool ApplyStudioDetails(ListedStudioDetails details, DateTime changedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Name);
        if (details.Latitude.HasValue != details.Longitude.HasValue)
            throw new ArgumentException("Latitude and longitude come together.", nameof(details));
        if (details.Latitude is { } lat && (double.IsNaN(lat) || lat is < -90 or > 90))
            throw new ArgumentOutOfRangeException(nameof(details), lat, "Latitude must be between -90 and 90.");
        if (details.Longitude is { } lng && (double.IsNaN(lng) || lng is < -180 or > 180))
            throw new ArgumentOutOfRangeException(nameof(details), lng, "Longitude must be between -180 and 180.");

        if (StudioDetailsChangedAt is { } current && changedAt < current) return false;

        Name = details.Name.Trim();
        Phone = Blank(details.Phone);
        Address = Blank(details.Address);
        City = Blank(details.City);
        Latitude = details.Latitude;
        Longitude = details.Longitude;
        StudioDetailsChangedAt = changedAt;

        if (!HasCompleteDetails) IsPublished = false;
        return true;
    }

    /// <summary>
    /// Shows the studio to customers. A studio with nothing to offer would only be an empty pin, and one without an
    /// address and a pin can't be placed on the map at all.
    /// </summary>
    public Result Publish()
    {
        if (_services.Count == 0) return ListingErrors.NoServices;
        if (!HasCompleteDetails) return ListingErrors.DetailsIncomplete;

        IsPublished = true;
        return Result.Ok();
    }

    public void Unpublish() => IsPublished = false;

    /// <summary>
    /// Replaces the copied catalogue with a newer snapshot. Returns false, changing nothing, for a snapshot older
    /// than the one already applied, since messages can arrive out of order.
    /// </summary>
    public bool ReplaceCatalog(
        IReadOnlyCollection<ListedService> services, IReadOnlyCollection<ListedPrice> prices, DateTime changedAt)
    {
        if (CatalogChangedAt is { } current && changedAt < current) return false;

        _services.Clear();
        _services.AddRange(services);
        _prices.Clear();
        _prices.AddRange(prices.Where(p => services.Any(s => s.ServiceId == p.ServiceId)));
        CatalogChangedAt = changedAt;
        return true;
    }

    public IReadOnlyList<ServiceCategory> Categories => _services.Select(s => s.Category).Distinct().Order().ToList();

    /// <summary>
    /// The lowest price for a car of this size, optionally within one category; null when nothing is priced for it.
    /// </summary>
    public long? FromPrice(VehicleSize size, ServiceCategory? category = null) =>
        _prices
            .Where(p => p.Size == size)
            .Where(p => category == null || _services.Any(s => s.ServiceId == p.ServiceId && s.Category == category))
            .Select(p => (long?)p.AmountInCents)
            .Min();

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// The studio's details as Identity owns them, copied onto its listing.
/// </summary>
public sealed record ListedStudioDetails(
    string Name, string? Phone, string? Address, string? City, double? Latitude, double? Longitude);

public sealed record ListedService(Guid ServiceId, string Name, ServiceCategory Category);

public sealed record ListedPrice(Guid ServiceId, VehicleSize Size, long AmountInCents);

public static class ListingErrors
{
    public static readonly Error NotFound =
        Error.NotFound("LISTING_NOT_FOUND", "Studio nie ma jeszcze profilu w wyszukiwarce.");

    public static readonly Error NoServices = Error.Validation(
        "LISTING_HAS_NO_SERVICES", "Dodaj przynajmniej jedną usługę, zanim opublikujesz studio.");

    public static readonly Error DetailsIncomplete = Error.Validation(
        "STUDIO_DETAILS_INCOMPLETE",
        "Uzupełnij adres, miasto i położenie studia na mapie w ustawieniach studia, zanim je opublikujesz.");
}
