using FluentValidation;

namespace Scheduler.Api.Contracts;

/// <summary>
/// Shape and format only (422 VALIDATION_FAILED). Business rules such as lead time and
/// opening hours live in BookingPolicy, because they need the dealership and the clock.
/// </summary>
public sealed class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    // 17 characters; I, O and Q are never used in a VIN (ISO 3779).
    private const string VinPattern = "^[A-HJ-NPR-Za-hj-npr-z0-9]{17}$";

    public BookAppointmentRequestValidator()
    {
        RuleFor(x => x.DealershipId).NotEmpty();
        RuleFor(x => x.ServiceTypeId).NotEmpty();
        RuleFor(x => x.StartTime).NotNull();
        RuleFor(x => x.Channel).NotNull().IsInEnum();

        RuleFor(x => x.Customer).NotNull();
        When(x => x.Customer is not null, () =>
        {
            RuleFor(x => x.Customer!.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Customer!.Email).NotEmpty().EmailAddress().MaximumLength(254);
            RuleFor(x => x.Customer!.Phone).NotEmpty().Matches(@"^\+?[0-9 ()-]{6,31}$")
                .WithMessage("Phone must contain 6-32 digits, spaces, brackets or dashes.");
        });

        RuleFor(x => x.Vehicle).NotNull();
        When(x => x.Vehicle is not null, () =>
        {
            RuleFor(x => x.Vehicle!.Vin).NotEmpty().Matches(VinPattern)
                .WithMessage("VIN must be 17 letters or digits, excluding I, O and Q.");
            RuleFor(x => x.Vehicle!.Make).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Vehicle!.Model).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Vehicle!.Year).InclusiveBetween(1950, 2100);
        });
    }
}
