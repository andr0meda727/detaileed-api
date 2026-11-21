using System.Collections.Concurrent;
using Identity.Application.Abstractions;

namespace IntegrationTests.Api;

/// <summary>
/// Stands in for Supabase Auth and records the invitations it would send.
/// </summary>
public sealed class FakeAuthProvider : IAuthProvider
{
    public ConcurrentBag<(string Email, string RedirectUrl)> Invites { get; } = [];

    public Task InviteUserAsync(string email, string redirectUrl, CancellationToken cancellationToken)
    {
        Invites.Add((email, redirectUrl));
        return Task.CompletedTask;
    }
}
