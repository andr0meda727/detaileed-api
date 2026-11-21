namespace Identity.Contracts;

// Integration events published by the Identity module. They carry everything a consumer needs,
// so consumers don't call back into Identity, and they stay plain records so they can travel over a broker.

/// <summary>
/// Someone was invited to join a studio. When <paramref name="HasAccount"/> is false the person has no login yet,
/// so the invitation goes out as an account invite; otherwise it is a normal email with the accept link.
/// <paramref name="StudioTimeZone"/> is the studio's IANA zone, for showing <paramref name="ExpiresAt"/> (UTC) in local time;
/// messages queued before it existed have none.
/// </summary>
public sealed record EmployeeInvited(
    Guid StudioId,
    Guid InvitationId,
    string Email,
    string StudioName,
    string AcceptUrl,
    DateTime ExpiresAt,
    bool HasAccount,
    string? StudioTimeZone = null);

/// <summary>
/// The owner changed the studio's details. Marketplace copies them onto the studio's public listing; snapshots can
/// arrive out of order, so a consumer keeps the one with the latest <paramref name="ChangedAt"/>.
/// </summary>
public sealed record StudioDetailsChanged(Guid StudioId, StudioDetails Details, DateTime ChangedAt);
