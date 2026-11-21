using System.Net.Http.Json;

namespace IntegrationTests.Api;

/// <summary>
/// Puts a studio on the customer map the way an owner does: its details in the studio settings first (the listing
/// copies them when it is created), then the listing, then publishing it. The client must be the studio's owner.
/// </summary>
public static class ListingSetup
{
    public static async Task SetStudioDetailsAsync(
        this HttpClient owner, string name = "Detail Studio", double latitude = 50.06, double longitude = 19.94,
        string city = "Kraków", string address = "ul. Długa 1", string? phone = null)
    {
        (await owner.PutAsJsonAsync("/api/studios/current", new
        {
            name, phone, address, city, latitude, longitude
        })).EnsureSuccessStatusCode();
    }

    public static async Task CreateListingAsync(
        this HttpClient owner, string name = "Detail Studio", double latitude = 50.06, double longitude = 19.94,
        string city = "Kraków", string address = "ul. Długa 1", string? phone = null, string? description = null)
    {
        await owner.SetStudioDetailsAsync(name, latitude, longitude, city, address, phone);
        (await owner.PutAsJsonAsync("/api/marketplace/listing", new { description })).EnsureSuccessStatusCode();
    }

    public static async Task PublishListingAsync(
        this HttpClient owner, string name = "Detail Studio", double latitude = 50.06, double longitude = 19.94,
        string city = "Kraków", string address = "ul. Długa 1", string? phone = null, string? description = null)
    {
        await owner.CreateListingAsync(name, latitude, longitude, city, address, phone, description);
        (await owner.PostAsync("/api/marketplace/listing/publish", null)).EnsureSuccessStatusCode();
    }
}
