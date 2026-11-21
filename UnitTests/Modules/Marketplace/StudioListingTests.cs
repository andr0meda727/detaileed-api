using Marketplace.Domain;
using Scheduling.Contracts;
using Shared.Kernel.Vehicles;

namespace Unit.Modules.Marketplace;

[TestFixture]
public class StudioListingTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly ListedStudioDetails Details =
        new(" Detail Studio ", "  ", "ul. Długa 1", " Kraków ", 50.0614, 19.9366);

    private static readonly ListedStudioDetails WithoutLocation = new("Detail Studio", null, null, null, null, null);

    private static readonly Guid Wash = Guid.NewGuid();
    private static readonly Guid Upholstery = Guid.NewGuid();

    private static StudioListing WithCatalog(ListedStudioDetails? details = null)
    {
        var listing = StudioListing.Create(Guid.NewGuid(), details ?? Details, Monday);
        listing.ReplaceCatalog(
            [new ListedService(Wash, "Mycie", ServiceCategory.ExteriorWash), new ListedService(Upholstery, "Pranie", ServiceCategory.Interior)],
            [
                new ListedPrice(Wash, VehicleSize.Medium, 8000), new ListedPrice(Wash, VehicleSize.Large, 10000),
                new ListedPrice(Upholstery, VehicleSize.Large, 35000)
            ],
            Monday);
        return listing;
    }

    [Test]
    public void Create_CopiesTheTrimmedDetailsAndStartsUnpublished()
    {
        var listing = StudioListing.Create(Guid.NewGuid(), Details, Monday);

        Assert.Multiple(() =>
        {
            Assert.That(listing.Name, Is.EqualTo("Detail Studio"));
            Assert.That(listing.Phone, Is.Null);
            Assert.That(listing.City, Is.EqualTo("Kraków"));
            Assert.That(listing.Description, Is.Null);
            Assert.That(listing.HasCompleteDetails, Is.True);
            Assert.That(listing.StudioDetailsChangedAt, Is.EqualTo(Monday));
            Assert.That(listing.IsPublished, Is.False);
        });
    }

    [Test]
    public void Create_AcceptsAStudioWithoutALocationYet()
    {
        var listing = StudioListing.Create(Guid.NewGuid(), WithoutLocation, Monday);

        Assert.That(listing.HasCompleteDetails, Is.False);
        Assert.That(listing.Address, Is.Null);
    }

    [TestCase(91.0, 19.0)]
    [TestCase(-91.0, 19.0)]
    [TestCase(50.0, 181.0)]
    [TestCase(50.0, -181.0)]
    [TestCase(double.NaN, 19.0)]
    public void Create_RejectsAPinOffTheMap(double latitude, double longitude)
    {
        Assert.That(
            () => StudioListing.Create(Guid.NewGuid(), Details with { Latitude = latitude, Longitude = longitude }, Monday),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Create_RejectsHalfAPin()
    {
        Assert.That(() => StudioListing.Create(Guid.NewGuid(), Details with { Longitude = null }, Monday),
            Throws.ArgumentException);
    }

    [Test]
    public void Publish_NeedsAtLeastOneService()
    {
        var empty = StudioListing.Create(Guid.NewGuid(), Details, Monday);
        var offering = WithCatalog();

        Assert.That(empty.Publish().Error, Is.EqualTo(ListingErrors.NoServices));
        Assert.That(empty.IsPublished, Is.False);
        Assert.That(offering.Publish().Success, Is.True);
        Assert.That(offering.IsPublished, Is.True);
    }

    [Test]
    public void Publish_NeedsTheAddressAndPin()
    {
        var listing = WithCatalog(WithoutLocation with { Address = "ul. Długa 1", City = "Kraków" });

        Assert.That(listing.Publish().Error, Is.EqualTo(ListingErrors.DetailsIncomplete));
        Assert.That(listing.IsPublished, Is.False);
    }

    [Test]
    public void UpdateDescription_LeavesTheCopiedDetailsAlone()
    {
        var listing = WithCatalog();

        listing.UpdateDescription("  Ceramika i pranie tapicerki ");

        Assert.That(listing.Description, Is.EqualTo("Ceramika i pranie tapicerki"));
        Assert.That(listing.Name, Is.EqualTo("Detail Studio"));
    }

    [Test]
    public void ApplyStudioDetails_TakesANewerSnapshot()
    {
        var listing = WithCatalog();

        var applied = listing.ApplyStudioDetails(Details with { Name = "Shine", Phone = "500 600 700" }, Monday.AddHours(1));

        Assert.That(applied, Is.True);
        Assert.That(listing.Name, Is.EqualTo("Shine"));
        Assert.That(listing.Phone, Is.EqualTo("500 600 700"));
        Assert.That(listing.StudioDetailsChangedAt, Is.EqualTo(Monday.AddHours(1)));
    }

    [Test]
    public void ApplyStudioDetails_IgnoresAnOlderSnapshot()
    {
        var listing = WithCatalog();

        var applied = listing.ApplyStudioDetails(Details with { Name = "Stara nazwa" }, Monday.AddMinutes(-1));

        Assert.That(applied, Is.False);
        Assert.That(listing.Name, Is.EqualTo("Detail Studio"));
    }

    [Test]
    public void ApplyStudioDetails_HidesAPublishedListingThatLostItsPin()
    {
        var listing = WithCatalog();
        listing.Publish();

        listing.ApplyStudioDetails(Details with { Latitude = null, Longitude = null }, Monday.AddHours(1));

        Assert.That(listing.IsPublished, Is.False);
        Assert.That(listing.HasCompleteDetails, Is.False);
    }

    [Test]
    public void ApplyStudioDetails_KeepsAPublishedListingPublishedWhenStillComplete()
    {
        var listing = WithCatalog();
        listing.Publish();

        listing.ApplyStudioDetails(Details with { Address = "ul. Krótka 2" }, Monday.AddHours(1));

        Assert.That(listing.IsPublished, Is.True);
    }

    [Test]
    public void ReplaceCatalog_IgnoresAnOlderSnapshot()
    {
        var listing = WithCatalog();

        var applied = listing.ReplaceCatalog([], [], Monday.AddMinutes(-1));

        Assert.That(applied, Is.False);
        Assert.That(listing.Services, Has.Count.EqualTo(2));
    }

    [Test]
    public void ReplaceCatalog_TakesANewerSnapshotAndDropsPricesOfMissingServices()
    {
        var listing = WithCatalog();

        listing.ReplaceCatalog([new ListedService(Wash, "Mycie", ServiceCategory.ExteriorWash)],
            [new ListedPrice(Wash, VehicleSize.Small, 6000), new ListedPrice(Upholstery, VehicleSize.Small, 1)], Monday.AddHours(1));

        Assert.That(listing.Services.Select(s => s.ServiceId), Is.EqualTo(new[] { Wash }));
        Assert.That(listing.Prices, Is.EqualTo(new[] { new ListedPrice(Wash, VehicleSize.Small, 6000) }));
        Assert.That(listing.CatalogChangedAt, Is.EqualTo(Monday.AddHours(1)));
    }

    [Test]
    public void FromPrice_IsTheCheapestForTheSize_OptionallyInOneCategory()
    {
        var listing = WithCatalog();

        Assert.Multiple(() =>
        {
            Assert.That(listing.FromPrice(VehicleSize.Large), Is.EqualTo(10000));
            Assert.That(listing.FromPrice(VehicleSize.Large, ServiceCategory.Interior), Is.EqualTo(35000));
            Assert.That(listing.FromPrice(VehicleSize.Small), Is.Null);
            Assert.That(listing.Categories, Is.EqualTo(new[] { ServiceCategory.ExteriorWash, ServiceCategory.Interior }));
        });
    }
}
