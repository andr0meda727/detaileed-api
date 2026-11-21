using FluentValidation;
using Identity.Domain.Studios;
using Shared.Application.Validation;
using Shared.Kernel.Time;

namespace Identity.Application;

// Request and response shapes of the identity endpoints; they match the earlier API

public class InviteEmployeeRequest
{
    public string Email { get; set; } = string.Empty;
    public long RoleId { get; set; }
}

public sealed record ChangeEmployeeRoleRequest(Guid UserId, long RoleId);

public sealed record UserStudioDto(Guid StudioId, string Name, string Role, string? Address, string TimeZone);

public sealed record ChangeStudioTimeZoneRequest(string TimeZone);

/// <summary>
/// The studio's details as the owner edits them in the studio settings. Name, phone, address, city and the pin are
/// copied onto the studio's public listing; the pin (latitude and longitude) is set or cleared as a pair.
/// </summary>
public sealed record ChangeStudioDetailsRequest(
    string Name, string? Phone, string? Email, string? Address, string? City, double? Latitude, double? Longitude);

public sealed record StudioDetailsDto(
    Guid StudioId, string Name, string? Phone, string? Email, string? Address, string? City,
    double? Latitude, double? Longitude, string TimeZone);

// TimeZone is an IANA name, e.g. "Europe/Warsaw"; Europe/Warsaw when left out
public sealed record CreateStudioRequest(
    string Name, string? Phone = null, string? Email = null, string? Address = null, string? TimeZone = null);

public sealed record StudioEmployeeDto(
    Guid UserId, string Email, string? FirstName, string? LastName, long RoleId, string RoleName);

public sealed record InvitationResponseDto(Guid Id, string StudioName, string Role, string Email, DateTime ExpiresAt);

public class InviteEmployeeRequestValidator : AbstractValidator<InviteEmployeeRequest>
{
    public InviteEmployeeRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Adres e-mail jest wymagany.")
            .EmailAddress().WithMessage("Podany adres e-mail jest nieprawidłowy.")
            .MaximumLength(254).WithMessage("Adres e-mail jest zbyt długi.")
            .Must(e => e == e.Trim()).WithMessage(
                "Adres e-mail nie może zawierać spacji na początku ani końcu.");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Rola jest wymagana.")
            .Must(Roles.IsAssignable).WithMessage(
                "Możesz zaprosić użytkownika tylko na rolę Pracownika lub Managera. " +
                "Nie można bezpośrednio przyznać roli Właściciela.");
    }
}

public class CreateStudioRequestValidator : AbstractValidator<CreateStudioRequest>
{
    public CreateStudioRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa studia jest wymagana.")
            .MaximumLength(100).WithMessage("Nazwa studia nie może przekraczać 100 znaków.");

        RuleFor(x => x.Phone)
            .ValidPhone()
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Podany adres e-mail jest nieprawidłowy.")
            .MaximumLength(254).WithMessage("Adres e-mail jest zbyt długi.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Address)
            .MaximumLength(200).WithMessage("Adres nie może przekraczać 200 znaków.");

        RuleFor(x => x.TimeZone)
            .Must(TimeZones.IsValid).WithMessage("Nieznana strefa czasowa. Podaj nazwę IANA, np. Europe/Warsaw.")
            .When(x => x.TimeZone != null);
    }
}

public class ChangeStudioDetailsRequestValidator : AbstractValidator<ChangeStudioDetailsRequest>
{
    public ChangeStudioDetailsRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nazwa studia jest wymagana.")
            .MaximumLength(100).WithMessage("Nazwa studia nie może przekraczać 100 znaków.");

        RuleFor(x => x.Phone)
            .ValidPhone()
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Podany adres e-mail jest nieprawidłowy.")
            .MaximumLength(254).WithMessage("Adres e-mail jest zbyt długi.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Address)
            .MaximumLength(200).WithMessage("Adres nie może przekraczać 200 znaków.");

        RuleFor(x => x.City)
            .MaximumLength(100).WithMessage("Miasto nie może przekraczać 100 znaków.");

        RuleFor(x => x.Latitude)
            .NotNull().When(x => x.Longitude != null).WithMessage("Zaznacz położenie studia na mapie.")
            .InclusiveBetween(-90, 90).WithMessage("Szerokość geograficzna musi być między -90 a 90.");

        RuleFor(x => x.Longitude)
            .NotNull().When(x => x.Latitude != null).WithMessage("Zaznacz położenie studia na mapie.")
            .InclusiveBetween(-180, 180).WithMessage("Długość geograficzna musi być między -180 a 180.");
    }
}

public class ChangeStudioTimeZoneRequestValidator : AbstractValidator<ChangeStudioTimeZoneRequest>
{
    public ChangeStudioTimeZoneRequestValidator()
    {
        RuleFor(x => x.TimeZone)
            .Must(TimeZones.IsValid).WithMessage("Nieznana strefa czasowa. Podaj nazwę IANA, np. Europe/Warsaw.");
    }
}
