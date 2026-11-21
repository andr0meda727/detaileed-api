namespace Shared.Api.RateLimiting;

/// <summary>
/// Names of the host's rate-limit policies, for <c>[EnableRateLimiting]</c> in any module.
/// </summary>
public static class RateLimits
{
    /// <summary>
    /// Anonymous public endpoints (the customer map): a fixed number of requests per minute per client IP,
    /// set by <c>RateLimiting:PublicPermitLimit</c>.
    /// </summary>
    public const string Public = "public";

    /// <summary>
    /// Sending chat messages: a fixed number per minute per signed-in user (customers behind one shared IP don't
    /// share it), set by <c>RateLimiting:ChatPermitLimit</c>.
    /// </summary>
    public const string Chat = "chat";
}
