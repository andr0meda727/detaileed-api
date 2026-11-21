using Identity.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace Identity.Infrastructure.Auth;

/// <summary>
/// Supabase Auth through its admin API (service-role key). Sign-up and sign-in don't pass through here: the frontends
/// call Supabase directly, behind its captcha and rate limits.
/// </summary>
public class SupabaseAuthProvider : IAuthProvider
{
    private readonly IGotrueAdminClient<User> _adminClient;

    public SupabaseAuthProvider(IConfiguration config)
    {
        var supabaseUrl = config["Supabase:Url"] ?? throw new InvalidOperationException("Supabase:Url is missing.");
        var serviceRoleKey = config["Supabase:ServiceRoleKey"]
                             ?? throw new InvalidOperationException("Supabase:ServiceRoleKey is missing.");

        _adminClient = new AdminClient(serviceRoleKey, new ClientOptions
        {
            Url = $"{supabaseUrl}/auth/v1",
            Headers = new Dictionary<string, string> { { "apikey", serviceRoleKey } }
        });
    }

    public async Task InviteUserAsync(string email, string redirectUrl, CancellationToken cancellationToken)
    {
        if (!await _adminClient.InviteUserByEmail(email, new InviteUserByEmailOptions { RedirectTo = redirectUrl }))
        {
            throw new InvalidOperationException("Supabase did not send the invitation.");
        }
    }
}
