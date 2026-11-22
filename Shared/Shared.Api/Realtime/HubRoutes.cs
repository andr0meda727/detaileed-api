namespace Shared.Api.Realtime;

/// <summary>
/// Where the host's SignalR hubs live. Requests under <see cref="Prefix"/> may carry the access token in the query
/// string (a browser can't set headers on a WebSocket), so every hub must be mapped under it, and only hubs.
/// </summary>
public static class HubRoutes
{
    public const string Prefix = "/hubs";
}
