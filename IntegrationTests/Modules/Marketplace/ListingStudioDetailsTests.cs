using System.Net.Http.Json;
using System.Text.Json;
using Identity.Contracts;
using IntegrationTests.Api;
using IntegrationTests.Infrastructure;
using Marketplace.Domain;
using Marketplace.Infrastructure.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Scheduling.Contracts;
using Shared.Infrastructure.Tenancy;

namespace IntegrationTests.Modules.Marketplace;

/// <summary>
/// The listing's copy of the studio's name, phone and location follows Identity through the outbox and the bus.
/// </summary>
[TestFixture]
public class ListingStudioDetailsTests
{
    private ApiFactory _factory = null!;
    private TwoStudiosSeed _seed = null!;

    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        _seed = new TwoStudiosSeed();
        await _seed.SeedAsync();
        _factory = new ApiFactory();
    }

    [OneTimeTearDown]
    public async Task TearDownAsync() => await _factory.DisposeAsync();

    [Test]
    public async Task ChangingTheStudioDetails_UpdatesItsListing_AndOnlyItsOwn()
    {
        using var ownerA = _factory.CreateClientFor(_seed.StudioA.OwnerUserId);
        using var ownerB = _factory.CreateClientFor(_seed.StudioB.OwnerUserId);
        await ownerA.CreateListingAsync("Studio A");
        await ownerB.CreateListingAsync("Studio B");

        await ownerA.SetStudioDetailsAsync("Shine Gdańsk", 54.352, 18.646, "Gdańsk", "ul. Krótka 2", "500 600 700");

        JsonElement listing = default;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            listing = await ownerA.GetFromJsonAsync<JsonElement>("/api/marketplace/listing");
            if (listing.GetProperty("name").GetString() == "Shine Gdańsk") break;
            await Task.Delay(100);
        }

        Assert.Multiple(() =>
        {
            Assert.That(listing.GetProperty("name").GetString(), Is.EqualTo("Shine Gdańsk"));
            Assert.That(listing.GetProperty("city").GetString(), Is.EqualTo("Gdańsk"));
            Assert.That(listing.GetProperty("address").GetString(), Is.EqualTo("ul. Krótka 2"));
            Assert.That(listing.GetProperty("phone").GetString(), Is.EqualTo("500 600 700"));
            Assert.That(listing.GetProperty("latitude").GetDouble(), Is.EqualTo(54.352));
        });
        var other = await ownerB.GetFromJsonAsync<JsonElement>("/api/marketplace/listing");
        Assert.That(other.GetProperty("name").GetString(), Is.EqualTo("Studio B"));
    }

    [Test]
    public async Task AnOlderSnapshot_DoesNotOverwriteANewerOne()
    {
        var studioId = await AddListingAsync(new ListedStudioDetails("Nowa nazwa", null, "ul. 1", "Kraków", 50, 19));

        await ConsumeAsync(new StudioDetailsChanged(studioId,
            new StudioDetails("Stara nazwa", null, "ul. 1", "Kraków", 50, 19), DateTime.UtcNow.AddMinutes(-5)));

        Assert.That((await LoadAsync(studioId)).Name, Is.EqualTo("Nowa nazwa"));
    }

    [Test]
    public async Task ARedeliveredSnapshot_ChangesNothingTwice()
    {
        var studioId = await AddListingAsync(new ListedStudioDetails("Studio", null, "ul. 1", "Kraków", 50, 19));
        var message = new StudioDetailsChanged(studioId,
            new StudioDetails("Shine", null, "ul. 1", "Kraków", 50, 19), DateTime.UtcNow.AddMinutes(1));

        await ConsumeAsync(message);
        await ConsumeAsync(message);

        Assert.That((await LoadAsync(studioId)).Name, Is.EqualTo("Shine"));
    }

    [Test]
    public async Task APublishedListingThatLosesItsPin_IsHidden()
    {
        var studioId = await AddListingAsync(new ListedStudioDetails("Studio", null, "ul. 1", "Kraków", 50, 19), publish: true);

        await ConsumeAsync(new StudioDetailsChanged(studioId,
            new StudioDetails("Studio", null, "ul. 1", "Kraków", null, null), DateTime.UtcNow.AddMinutes(1)));

        var listing = await LoadAsync(studioId);
        Assert.That(listing.IsPublished, Is.False);
        Assert.That(listing.Latitude, Is.Null);
    }

    [Test]
    public async Task AStudioWithoutAListing_IsSkipped()
    {
        var studioId = Guid.NewGuid();

        await ConsumeAsync(new StudioDetailsChanged(studioId,
            new StudioDetails("Studio", null, null, null, null, null), DateTime.UtcNow));

        await using var db = PostgresFixture.CreateMarketplaceDbContext(new TenantContext());
        Assert.That(await db.Listings.AnyAsync(l => l.StudioId == studioId), Is.False);
    }

    private static async Task<Guid> AddListingAsync(ListedStudioDetails details, bool publish = false)
    {
        var studioId = Guid.NewGuid();
        await using var db = PostgresFixture.CreateMarketplaceDbContext(new TenantContext());
        var listing = StudioListing.Create(studioId, details, DateTime.UtcNow);
        if (publish)
        {
            listing.ReplaceCatalog([new ListedService(Guid.NewGuid(), "Mycie", ServiceCategory.ExteriorWash)],
                [], DateTime.UtcNow);
            listing.Publish();
        }
        db.Listings.Add(listing);
        await db.SaveChangesAsync();
        return studioId;
    }

    private static async Task<StudioListing> LoadAsync(Guid studioId)
    {
        await using var db = PostgresFixture.CreateMarketplaceDbContext(new TenantContext());
        return await db.Listings.AsNoTracking().SingleAsync(l => l.StudioId == studioId);
    }

    private static async Task ConsumeAsync(StudioDetailsChanged message)
    {
        await using var db = PostgresFixture.CreateMarketplaceDbContext(new TenantContext());
        var context = Substitute.For<ConsumeContext<StudioDetailsChanged>>();
        context.Message.Returns(message);

        await new ListingStudioDetailsConsumer(db).Consume(context);
    }
}
