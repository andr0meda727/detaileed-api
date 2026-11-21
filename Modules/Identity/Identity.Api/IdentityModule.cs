using System.Reflection;
using System.Security.Claims;
using FluentValidation;
using Identity.Api.Controllers;
using Identity.Application;
using Identity.Application.Abstractions;
using Identity.Application.Directory;
using Identity.Application.Tenancy;
using Identity.Contracts;
using Identity.Domain.Studios;
using Identity.Infrastructure;
using Identity.Infrastructure.Consumers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Shared.Api.Authorization;
using Shared.Api.Realtime;
using Shared.Infrastructure.Tenancy;
using Shared.Kernel.Tenancy;

namespace Identity.Api;

/// <summary>
/// Composition root of the Identity module: sign-in (Supabase JWTs), tenancy, studios, staff and invitations.
/// The host calls <see cref="AddIdentityModule"/>, adds <see cref="Tenancy.TenantMiddlewareExtensions.UseTenantContext"/>
/// after authentication, and passes <see cref="ConsumersAssembly"/> to the message bus.
/// </summary>
public static class IdentityModule
{
    public const string OwnerOnlyPolicy = Policies.OwnerOnly;

    public static Assembly ConsumersAssembly => typeof(AccountInviteConsumer).Assembly;

    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        // One per request, filled by the tenant middleware and read by every module's DbContext
        services.AddScoped<ITenantContext, TenantContext>();

        services.AddScoped<ResolveCallerHandler>();

        // Other modules read studio facts (the time zone) only through this
        services.AddScoped<IStudioDirectory, StudioDirectory>();

        var frontendBaseUrl = configuration["Frontend:BaseUrl"]
                              ?? throw new InvalidOperationException("Frontend:BaseUrl is missing.");
        services.AddSingleton(new FrontendSettings(frontendBaseUrl));
        services.TryAddSingleton(TimeProvider.System);

        services.AddValidatorsFromAssemblyContaining<CreateStudioRequestValidator>();

        services.AddIdentityInfrastructure(configuration);

        AddSupabaseAuthentication(services, configuration);

        services.AddControllers().AddApplicationPart(typeof(StudiosController).Assembly);

        return services;
    }

    private static void AddSupabaseAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var supabaseUrl = configuration["Supabase:Url"] ?? throw new InvalidOperationException("Supabase:Url is missing.");
        var authority = $"{supabaseUrl}/auth/v1";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = true,
                    ValidAudience = "authenticated",
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.Zero
                };

                // A browser can't add a header to a WebSocket (or server-sent events), so SignalR sends the token as
                // ?access_token=. Accept it there only for the hubs; everything else needs the header. The hosting
                // log category stays at Warning (appsettings.json), because its request lines include the query.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.HttpContext.Request.Path.StartsWithSegments(HubRoutes.Prefix)
                            && context.Request.Query["access_token"] is { Count: 1 } token)
                        {
                            context.Token = token.ToString();
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        // The role claim is added by the tenant middleware for the resolved studio
        services.AddAuthorization(options =>
        {
            options.AddPolicy(OwnerOnlyPolicy, policy => policy.RequireClaim(ClaimTypes.Role, Roles.Owner.ToString()));
        });
    }
}
