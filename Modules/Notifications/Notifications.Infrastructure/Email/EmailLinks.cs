namespace Notifications.Infrastructure.Email;

/// <summary>
/// Where emails link to: <paramref name="DashboardBaseUrl"/> is the studio dashboard (Frontend:BaseUrl),
/// <paramref name="WebBaseUrl"/> the customer site (Web:BaseUrl). The paths are the frontends' routes.
/// </summary>
public sealed record EmailLinks(string DashboardBaseUrl, string WebBaseUrl)
{
    /// <summary>The request on the dashboard, in the studio it was sent to.</summary>
    public string BookingRequest(Guid studioId, Guid requestId) =>
        $"{DashboardBaseUrl.TrimEnd('/')}/s/{studioId}/requests?id={requestId}";

    /// <summary>The customer's own bookings and requests.</summary>
    public string CustomerBookings() => $"{WebBaseUrl.TrimEnd('/')}/account/bookings";
}
