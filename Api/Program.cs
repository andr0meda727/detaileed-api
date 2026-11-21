using System.Text.Json.Serialization;
using Api.Extensions;
using Api.Middleware;
using Chat.Api;
using Crm.Api;
using Customers.Api;
using Identity.Api;
using Identity.Api.Tenancy;
using Infrastructure.Extensions;
using Inventory.Api;
using Marketplace.Api;
using Notifications.Api;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Scheduling.Api;
using Shared.Infrastructure.Messaging;
using Shared.Kernel.Time;

var builder = WebApplication.CreateBuilder(args);

// Enums travel as their names ("Interior"), not numbers, so the API stays readable and reordering them is safe
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// The OpenAPI generator reads these (minimal-API) options, not the MVC ones above; without the converter here the
// spec types enums as integers although the API sends names.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Add Infrastructure (Database, Repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Add Application Services
builder.Services.AddApplicationServices();

// Modules
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);
builder.Services.AddCrmModule(builder.Configuration);
builder.Services.AddSchedulingModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddMarketplaceModule(builder.Configuration);
builder.Services.AddCustomersModule(builder.Configuration);
builder.Services.AddChatModule(builder.Configuration);

// One bus for the host; every assembly with consumers is listed here
builder.Services.AddMessaging(builder.Configuration,
    IdentityModule.ConsumersAssembly, SchedulingModule.ConsumersAssembly, InventoryModule.ConsumersAssembly, NotificationsModule.ConsumersAssembly,
    MarketplaceModule.ConsumersAssembly);

builder.Services.AddProxySupport();
builder.Services.AddRateLimits(builder.Configuration);

builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// The dashboard and the customer site; each environment lists its own origins (none listed = no cross-origin calls)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Fail at startup, not on the first report, if the OS has no time-zone data (Alpine images need tzdata)
TimeZones.Find(TimeZones.Default);

// Build-time OpenAPI generation (dotnet-getdocument, see Api.csproj) starts the host to read the endpoints.
// It needs no database, so the bus and the background workers are left out.
if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
{
    builder.Services.RemoveAll<IHostedService>();
}

var app = builder.Build();

// First, so everything after it (rate limits, HTTPS redirection, logs) sees the client's real IP and scheme
app.UseForwardedHeaders();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    // app.UseHsts(); allow it later for prod
    // app.UseExceptionHandler();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors("Frontend");

// Before the rate limiter, which limits chat per signed-in user. Checking a token needs no database, so
// anonymous public requests lose nothing.
app.UseAuthentication();

app.UseRateLimiter();

// After the rate limiter, so cached answers still count; before the tenant context, so a hit skips the profile lookup
app.UseOutputCache();

// Needs the routed endpoint (for [AllowWithoutStudio]) and the authenticated user
app.UseTenantContext();

app.UseAuthorization();

app.MapControllers();

app.MapChatHub();

app.Run();