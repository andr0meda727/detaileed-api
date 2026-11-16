using Identity.Contracts;
using Shared.Kernel.Time;

namespace Marketplace.Application.Listings;

internal static class StudioTimeZone
{
    /// <summary>
    /// The studio's IANA zone for its listing; <see cref="TimeZones.Default"/> when Identity has none.
    /// The public studio page is output-cached and Marketplace isn't told when the zone changes, so a change shows
    /// there only when the cached answer expires (60 s); zones change rarely, so that is acceptable.
    /// </summary>
    public static async Task<string> OfAsync(
        this IStudioDirectory studios, Guid studioId, CancellationToken cancellationToken) =>
        await studios.FindTimeZoneAsync(studioId, cancellationToken) ?? TimeZones.Default;
}
