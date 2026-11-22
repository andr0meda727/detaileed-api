namespace Identity.Contracts;

/// <summary>
/// Read-only facts about studios that other modules need at request time.
/// </summary>
public interface IStudioDirectory
{
    /// <summary>
    /// The studio's IANA time zone, e.g. "Europe/Warsaw", or null when there is no such studio.
    /// </summary>
    Task<string?> FindTimeZoneAsync(Guid studioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Where to tell the studio about something that needs an answer: the emails of its active owners.
    /// </summary>
    Task<IReadOnlyList<string>> GetOwnerEmailsAsync(Guid studioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The studio's current public details, or null when there is no such studio. Later changes are published as
    /// <see cref="StudioDetailsChanged"/>.
    /// </summary>
    Task<StudioDetails?> FindDetailsAsync(Guid studioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the user currently works at the studio (an active membership, any role).
    /// </summary>
    Task<bool> IsActiveMemberAsync(Guid studioId, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// What customers may see about a studio: its name, phone and where it is. The pin (latitude and longitude) is
/// either complete or absent.
/// </summary>
public sealed record StudioDetails(
    string Name, string? Phone, string? Address, string? City, double? Latitude, double? Longitude);
