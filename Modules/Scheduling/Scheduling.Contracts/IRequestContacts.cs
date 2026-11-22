namespace Scheduling.Contracts;

/// <summary>
/// Who sent a studio a booking request, for a module that lets the studio get back to them (the chat). The studio
/// is passed in, so a request is only found within the studio it was sent to.
/// </summary>
public interface IRequestContacts
{
    /// <summary>
    /// The customer behind the studio's request, or null when the studio has no such request.
    /// </summary>
    Task<RequestCustomerSummary?> FindRequestCustomerAsync(
        Guid studioId, Guid requestId, CancellationToken cancellationToken = default);
}

/// <summary>
/// <paramref name="Name"/> is the name the customer gave when they sent the request.
/// </summary>
public sealed record RequestCustomerSummary(Guid UserId, string Name);
