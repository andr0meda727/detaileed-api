using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Chat.Api.Realtime;

/// <summary>
/// A connection's SignalR user is the account id, read like <c>TenantMiddleware</c> does: the JWT handler maps
/// Supabase's "sub" to NameIdentifier, while a raw "sub" (e.g. the test sign-in) is taken as it is.
/// </summary>
internal sealed class ChatUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? connection.User.FindFirst("sub")?.Value;
}
