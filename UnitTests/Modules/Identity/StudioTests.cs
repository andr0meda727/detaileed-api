using Identity.Domain.Studios;

namespace Unit.Modules.Identity;

[TestFixture]
public class StudioTests
{
    private static readonly Guid Owner = Guid.NewGuid();

    private static Studio NewStudio() => Studio.Register(" Detail Studio ", null, " ", "Kraków", Owner);

    [Test]
    public void Register_MakesTheOwnerAnActiveMember()
    {
        var studio = NewStudio();

        var owner = studio.Members.Single();
        Assert.That(studio.Name, Is.EqualTo("Detail Studio"));
        Assert.That(studio.Email, Is.Null);
        Assert.That(owner, Has.Property(nameof(StudioMember.UserId)).EqualTo(Owner)
            .And.Property(nameof(StudioMember.RoleId)).EqualTo(Roles.Owner)
            .And.Property(nameof(StudioMember.IsActive)).True);
    }

    [Test]
    public void RemovedMember_IsBroughtBackWithTheNewRole()
    {
        var studio = NewStudio();
        var employee = Guid.NewGuid();
        studio.Admit(employee, Roles.Employee);
        studio.RemoveMember(employee, Owner);

        studio.Admit(employee, Roles.Manager);

        var member = studio.FindMember(employee)!;
        Assert.That(member.IsActive, Is.True);
        Assert.That(member.RoleId, Is.EqualTo(Roles.Manager));
        Assert.That(studio.Members, Has.Count.EqualTo(2));
    }

    [Test]
    public void Admit_CannotHandOutTheOwnerRole()
    {
        Assert.That(() => NewStudio().Admit(Guid.NewGuid(), Roles.Owner), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void RemoveMember_RejectsSelfOwnerAndUnknownPeople()
    {
        var studio = NewStudio();
        var manager = Guid.NewGuid();
        studio.Admit(manager, Roles.Manager);

        Assert.Multiple(() =>
        {
            Assert.That(studio.RemoveMember(Owner, Owner).Error, Is.EqualTo(StudioErrors.SelfRemove));
            Assert.That(studio.RemoveMember(Owner, manager).Error, Is.EqualTo(StudioErrors.CannotRemoveOwner));
            Assert.That(studio.RemoveMember(Guid.NewGuid(), Owner).Error, Is.EqualTo(StudioErrors.MemberNotFound));
        });
    }

    [Test]
    public void RemoveMember_DeactivatesAndCannotRemoveTwice()
    {
        var studio = NewStudio();
        var employee = Guid.NewGuid();
        studio.Admit(employee, Roles.Employee);

        Assert.That(studio.RemoveMember(employee, Owner).Success, Is.True);
        Assert.That(studio.FindMember(employee)!.IsActive, Is.False);
        Assert.That(studio.RemoveMember(employee, Owner).Error, Is.EqualTo(StudioErrors.MemberNotFound));
    }

    [Test]
    public void ChangeRole_OnlyBetweenAssignableRoles()
    {
        var studio = NewStudio();
        var employee = Guid.NewGuid();
        studio.Admit(employee, Roles.Employee);

        Assert.Multiple(() =>
        {
            Assert.That(studio.ChangeRole(employee, Roles.Owner, Owner).Error, Is.EqualTo(StudioErrors.InvalidRole));
            Assert.That(studio.ChangeRole(Owner, Roles.Manager, employee).Error, Is.EqualTo(StudioErrors.CannotChangeOwnerRole));
            Assert.That(studio.ChangeRole(Owner, Roles.Manager, Owner).Error, Is.EqualTo(StudioErrors.SelfRoleChange));
            Assert.That(studio.ChangeRole(employee, Roles.Manager, Owner).Success, Is.True);
        });
        Assert.That(studio.FindMember(employee)!.RoleId, Is.EqualTo(Roles.Manager));
    }

    [Test]
    public void Register_DefaultsToWarsawTime()
    {
        Assert.That(NewStudio().TimeZone, Is.EqualTo("Europe/Warsaw"));
        Assert.That(Studio.Register("X", null, null, null, Owner, "Europe/London").TimeZone, Is.EqualTo("Europe/London"));
    }

    [Test]
    public void Register_RejectsAnUnknownTimeZone()
    {
        Assert.That(() => Studio.Register("X", null, null, null, Owner, "Poland/Krakow"), Throws.ArgumentException);
    }

    [Test]
    public void ChangeTimeZone_AcceptsOnlyIanaNames()
    {
        var studio = NewStudio();

        Assert.That(studio.ChangeTimeZone("Central European Standard Time").Error, Is.EqualTo(StudioErrors.InvalidTimeZone));
        Assert.That(studio.TimeZone, Is.EqualTo("Europe/Warsaw"));

        Assert.That(studio.ChangeTimeZone("America/New_York").Success, Is.True);
        Assert.That(studio.TimeZone, Is.EqualTo("America/New_York"));
    }

    [Test]
    public void ChangeDetails_TrimsAndClearsBlankValues()
    {
        var studio = NewStudio();

        var result = studio.ChangeDetails(" Shine ", " 500 600 700 ", " ", " ul. Długa 1 ", " Kraków ", 50.06, 19.94);

        Assert.That(result.Success, Is.True);
        Assert.That(studio, Has.Property(nameof(Studio.Name)).EqualTo("Shine")
            .And.Property(nameof(Studio.Phone)).EqualTo("500 600 700")
            .And.Property(nameof(Studio.Email)).Null
            .And.Property(nameof(Studio.Address)).EqualTo("ul. Długa 1")
            .And.Property(nameof(Studio.City)).EqualTo("Kraków")
            .And.Property(nameof(Studio.Latitude)).EqualTo(50.06)
            .And.Property(nameof(Studio.Longitude)).EqualTo(19.94));
    }

    [Test]
    public void ChangeDetails_CanClearTheLocation()
    {
        var studio = NewStudio();
        studio.ChangeDetails("Shine", null, null, "ul. Długa 1", "Kraków", 50.06, 19.94);

        Assert.That(studio.ChangeDetails("Shine", null, null, null, null, null, null).Success, Is.True);
        Assert.That(studio.Latitude, Is.Null);
        Assert.That(studio.Longitude, Is.Null);
    }

    [TestCase(50.0, null)]
    [TestCase(null, 19.0)]
    [TestCase(91.0, 19.0)]
    [TestCase(50.0, -181.0)]
    [TestCase(double.NaN, 19.0)]
    public void ChangeDetails_RejectsAHalfOrOutOfRangePin(double? latitude, double? longitude)
    {
        var studio = NewStudio();

        var result = studio.ChangeDetails("Shine", null, null, null, null, latitude, longitude);

        Assert.That(result.Error, Is.EqualTo(StudioErrors.InvalidLocation));
        Assert.That(studio.Name, Is.EqualTo("Detail Studio"));
    }

    [Test]
    public void ChangeDetails_RequiresAName()
    {
        Assert.That(NewStudio().ChangeDetails(" ", null, null, null, null, null, null).Error,
            Is.EqualTo(StudioErrors.NameRequired));
    }
}
