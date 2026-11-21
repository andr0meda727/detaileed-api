using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Domain.Studios;
using Identity.Domain.Users;
using IntegrationTests.Api;
using IntegrationTests.Infrastructure;
using Shared.Infrastructure.Tenancy;

namespace IntegrationTests.Modules.Marketplace;

/// <summary>
/// The owner's side of the studio listing: create, edit, publish.
/// </summary>
[TestFixture]
public class ListingEndpointTests
{
    private ApiFactory _factory = null!;
    private TwoStudiosSeed _seed = null!;

    private static readonly object Description = new { description = "Mycie i ceramika" };

    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        _seed = new TwoStudiosSeed();
        await _seed.SeedAsync();
        _factory = new ApiFactory();
    }

    [OneTimeTearDown]
    public async Task TearDownAsync() => await _factory.DisposeAsync();

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("code", out var code) ? code.GetString() : null;

    private async Task<Guid> AddEmployeeAsync(Guid studioId)
    {
        var userId = Guid.NewGuid();
        await using var db = PostgresFixture.CreateIdentityDbContext(new TenantContext());
        db.Users.Add(new User(userId, $"{userId:N}@test.pl", null, null));
        var studio = await db.Studios.FindAsync(studioId);
        await db.Entry(studio!).Collection(s => s.Members).LoadAsync();
        studio!.Admit(userId, Roles.Employee);
        await db.SaveChangesAsync();
        return userId;
    }

    [Test]
    public async Task Owner_CreatesTheListing_WithTheStudiosDetailsAndServices_Unpublished()
    {
        using var owner = _factory.CreateClientFor(_seed.StudioA.OwnerUserId);
        await owner.SetStudioDetailsAsync("Detail Studio Kraków", 50.0614, 19.9366, phone: "500600700");

        var before = await owner.GetAsync("/api/marketplace/listing");
        var saved = await owner.PutAsJsonAsync("/api/marketplace/listing", Description);

        Assert.That(before.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await ProblemCodeAsync(before), Is.EqualTo("LISTING_NOT_FOUND"));
        Assert.That(saved.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var listing = await owner.GetFromJsonAsync<JsonElement>("/api/marketplace/listing");
        Assert.Multiple(() =>
        {
            Assert.That(listing.GetProperty("name").GetString(), Is.EqualTo("Detail Studio Kraków"));
            Assert.That(listing.GetProperty("phone").GetString(), Is.EqualTo("500600700"));
            Assert.That(listing.GetProperty("city").GetString(), Is.EqualTo("Kraków"));
            Assert.That(listing.GetProperty("description").GetString(), Is.EqualTo("Mycie i ceramika"));
            Assert.That(listing.GetProperty("hasCompleteDetails").GetBoolean(), Is.True);
            Assert.That(listing.GetProperty("isPublished").GetBoolean(), Is.False);
        });
        Assert.That(listing.GetProperty("services").EnumerateArray().Select(s => s.GetProperty("id").GetGuid()),
            Is.EqualTo(new[] { _seed.StudioA.ServiceId }));

        // Publishing shows it; it has a service, so that's allowed
        var publish = await owner.PostAsync("/api/marketplace/listing/publish", null);
        Assert.That(publish.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var published = await owner.GetFromJsonAsync<JsonElement>("/api/marketplace/listing");
        Assert.That(published.GetProperty("isPublished").GetBoolean(), Is.True);
    }

    [Test]
    public async Task Listing_CarriesTheStudiosTimeZone()
    {
        // Studios of its own, so the other tests still find theirs without a listing
        var seed = new TwoStudiosSeed();
        await seed.SeedAsync();
        using var owner = _factory.CreateClientFor(seed.StudioA.OwnerUserId);

        var saved = await owner.PutAsJsonAsync("/api/marketplace/listing", Description);
        (await owner.PutAsJsonAsync("/api/studios/time-zone", new { timeZone = "America/New_York" })).EnsureSuccessStatusCode();
        var listing = await owner.GetFromJsonAsync<JsonElement>("/api/marketplace/listing");

        Assert.That((await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("timeZone").GetString(),
            Is.EqualTo("Europe/Warsaw"));
        Assert.That(listing.GetProperty("timeZone").GetString(), Is.EqualTo("America/New_York"));
    }

    [Test]
    public async Task StudioWithoutServices_CannotBePublished()
    {
        using var owner = _factory.CreateClientFor(_seed.StudioB.OwnerUserId);
        await owner.DeleteAsync($"/api/service/{_seed.StudioB.ServiceId}");
        await owner.CreateListingAsync();

        var publish = await owner.PostAsync("/api/marketplace/listing/publish", null);

        Assert.That(publish.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await ProblemCodeAsync(publish), Is.EqualTo("LISTING_HAS_NO_SERVICES"));
    }

    [Test]
    public async Task StudioWithoutAnAddressAndPin_CanPrepareButNotPublishItsListing()
    {
        // Seeded studios have no address yet, as most do right after sign-up
        var seed = new TwoStudiosSeed();
        await seed.SeedAsync();
        using var owner = _factory.CreateClientFor(seed.StudioA.OwnerUserId);

        var saved = await owner.PutAsJsonAsync("/api/marketplace/listing", Description);
        var publish = await owner.PostAsync("/api/marketplace/listing/publish", null);

        Assert.That(saved.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var listing = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(listing.GetProperty("hasCompleteDetails").GetBoolean(), Is.False);
        Assert.That(listing.GetProperty("latitude").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(publish.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await ProblemCodeAsync(publish), Is.EqualTo("STUDIO_DETAILS_INCOMPLETE"));
    }

    [Test]
    public async Task SavingTheListing_ChangesOnlyItsDescription()
    {
        var seed = new TwoStudiosSeed();
        await seed.SeedAsync();
        using var owner = _factory.CreateClientFor(seed.StudioB.OwnerUserId);
        await owner.CreateListingAsync("Detail Studio");

        var saved = await owner.PutAsJsonAsync("/api/marketplace/listing", new { name = "Inna nazwa", description = "Nowy opis" });

        var listing = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(listing.GetProperty("name").GetString(), Is.EqualTo("Detail Studio"));
        Assert.That(listing.GetProperty("description").GetString(), Is.EqualTo("Nowy opis"));
    }

    [Test]
    public async Task Employee_CanSeeButNotChangeTheListing()
    {
        using var employee = _factory.CreateClientFor(await AddEmployeeAsync(_seed.StudioA.StudioId));

        var save = await employee.PutAsJsonAsync("/api/marketplace/listing", Description);
        var publish = await employee.PostAsync("/api/marketplace/listing/publish", null);

        Assert.That(save.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(publish.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task ATooLongDescription_IsAValidationProblem()
    {
        using var owner = _factory.CreateClientFor(_seed.StudioA.OwnerUserId);

        var response = await owner.PutAsJsonAsync("/api/marketplace/listing", new { description = new string('x', 2001) });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.That(errors.EnumerateObject().Select(e => e.Name), Is.EquivalentTo(new[] { "Description" }));
    }
}
