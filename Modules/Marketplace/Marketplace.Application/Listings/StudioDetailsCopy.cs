using Identity.Contracts;
using Marketplace.Domain;

namespace Marketplace.Application.Listings;

public static class StudioDetailsCopy
{
    /// <summary>
    /// Identity's studio details as the listing keeps them.
    /// </summary>
    public static ListedStudioDetails ToListed(this StudioDetails details) =>
        new(details.Name, details.Phone, details.Address, details.City, details.Latitude, details.Longitude);
}
