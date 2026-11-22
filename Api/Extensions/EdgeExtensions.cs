using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Shared.Api.RateLimiting;

namespace Api.Extensions;

/// <summary>
/// How the host meets the internet: the reverse proxy in front of it, and limits on anonymous traffic.
/// </summary>
public static class EdgeExtensions
{
    /// <summary>
    /// In production the API listens on 127.0.0.1 behind a reverse proxy (and Docker's network), so every request
    /// seems to come from the proxy. The client's real IP, needed for per-client rate limits, is taken from
    /// X-Forwarded-For, but only when the request comes from a loopback or private address: the API is not
    /// reachable from the internet directly, so only the proxy can set that header.
    /// </summary>
    public static IServiceCollection AddProxySupport(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var network in new[] { "127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "::1/128" })
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }

    /// <summary>
    /// The host's rate-limit policies; over the limit is 429 Too Many Requests.
    /// <list type="bullet">
    /// <item><see cref="RateLimits.Public"/>: <c>RateLimiting:PublicPermitLimit</c> requests per minute per client IP
    /// (120 by default, enough to pan the map freely).</item>
    /// <item><see cref="RateLimits.Chat"/>: <c>RateLimiting:ChatPermitLimit</c> messages per minute per signed-in user
    /// (30 by default, far above anyone typing). It needs the authenticated user, so the rate limiter runs after
    /// authentication; a request without one falls back to its IP.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var publicLimit = configuration.GetValue("RateLimiting:PublicPermitLimit", 120);
        var chatLimit = configuration.GetValue("RateLimiting:ChatPermitLimit", 30);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimits.Public, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = publicLimit, Window = TimeSpan.FromMinutes(1) }));
            options.AddPolicy(RateLimits.Chat, context => RateLimitPartition.GetFixedWindowLimiter(
                UserId(context) is { } userId ? $"user:{userId}" : $"ip:{ClientIp(context)}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = chatLimit, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();

    // The validated token's subject, as TenantMiddleware reads it
    private static string? UserId(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value
            : null;
}
