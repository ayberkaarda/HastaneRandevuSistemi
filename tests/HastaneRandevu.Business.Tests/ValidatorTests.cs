using FluentValidation;
using HastaneRandevu.Business.Security;
using HastaneRandevu.Business.Services;
using HastaneRandevu.Business.Validation;
using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Business.Tests;

/// <summary>Boundaries of the central validators and their Turkish messages.</summary>
public sealed class ValidatorTests
{
    private const string ValidNationalId = "10000000450";

    // Wednesday 4 March 2026, 10:10: day 90 of the booking window (2 June) is a Tuesday.
    private static readonly DateTime Now = new(2026, 3, 4, 10, 10, 0);

    [Theory]
    [InlineData(PasswordPolicy.MinLength - 1, false)]
    [InlineData(PasswordPolicy.MinLength, true)]
    [InlineData(PasswordPolicy.MaxLength, true)]
    [InlineData(PasswordPolicy.MaxLength + 1, false)]
    public void Registration_PasswordLength_IsEightToSixtyFour(int length, bool valid)
    {
        var input = RegistrationInput.Create("Deniz Örnek", ValidNationalId, new string('x', length));

        AssertResult(new RegistrationInputValidator(), input, valid, ValidationMessages.PasswordInvalid);
    }

    [Fact]
    public void Registration_MissingPassword_IsRejected() =>
        AssertResult(new RegistrationInputValidator(), RegistrationInput.Create("Deniz Örnek", ValidNationalId, null), false, ValidationMessages.PasswordInvalid);

    [Fact]
    public void Registration_PasswordIsNotTrimmed()
    {
        // Seven characters with surrounding spaces count as nine: what is typed is what is hashed.
        var input = RegistrationInput.Create("Deniz Örnek", ValidNationalId, " 1234567 ");

        Assert.Equal(" 1234567 ", input.Password);
        AssertResult(new RegistrationInputValidator(), input, true, ValidationMessages.PasswordInvalid);
    }

    [Theory]
    [InlineData(RegistrationInputValidator.FullNameMaxLength, true)]
    [InlineData(RegistrationInputValidator.FullNameMaxLength + 1, false)]
    public void Registration_NameLength_IsAtMostEighty(int length, bool valid)
    {
        var input = RegistrationInput.Create(new string('a', length), ValidNationalId, "Gizli.Sifre1");

        AssertResult(new RegistrationInputValidator(), input, valid, ValidationMessages.FullNameInvalid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Registration_BlankName_IsRejected(string? name) =>
        AssertResult(new RegistrationInputValidator(), RegistrationInput.Create(name, ValidNationalId, "Gizli.Sifre1"), false, ValidationMessages.FullNameInvalid);

    [Fact]
    public void Registration_TrimsNameAndNationalId()
    {
        var input = RegistrationInput.Create("  Deniz Örnek ", $" {ValidNationalId}  ", "Gizli.Sifre1");

        Assert.Equal("Deniz Örnek", input.FullName);
        Assert.Equal(ValidNationalId, input.NationalId);
        Assert.True(new RegistrationInputValidator().Validate(input).IsValid);
    }

    [Theory]
    [InlineData("10000000451")] // wrong eleventh digit
    [InlineData("10000000461")] // wrong tenth digit
    [InlineData("00000000000")] // first digit zero
    [InlineData("1000000045")] // ten digits
    [InlineData("1000000045a")] // not a digit
    public void Registration_InvalidNationalId_IsRejected(string nationalId) =>
        AssertResult(new RegistrationInputValidator(), RegistrationInput.Create("Deniz Örnek", nationalId, "Gizli.Sifre1"), false, ValidationMessages.NationalIdInvalid);

    [Fact]
    public void Registration_SeveralInvalidFields_ReportsOnlyTheFirstInTheOriginalOrder()
    {
        var result = new RegistrationInputValidator().Validate(RegistrationInput.Create("", "1", "x"));

        var failure = Assert.Single(result.Errors);
        Assert.Equal(ValidationMessages.FullNameInvalid, failure.ErrorMessage);
    }

    [Theory]
    [InlineData(PasswordPolicy.SignInMaxLength, true)]
    [InlineData(PasswordPolicy.SignInMaxLength + 1, false)]
    public void PatientSignIn_PasswordLength_IsBoundedByTheLegacyColumnOnly(int length, bool valid)
    {
        var input = PatientSignInInput.Create(ValidNationalId, new string('x', length));

        AssertResult(new PatientSignInInputValidator(), input, valid, ValidationMessages.PatientInputRequired);
    }

    [Fact]
    public void PatientSignIn_ShortLegacyPassword_IsAccepted() =>
        AssertResult(new PatientSignInInputValidator(), PatientSignInInput.Create(ValidNationalId, "1"), true, ValidationMessages.PatientInputRequired);

    [Fact]
    public void PatientSignIn_ChecksTheFormatButNotTheChecksum() =>
        AssertResult(new PatientSignInInputValidator(), PatientSignInInput.Create(" 12345678901 ", "Gizli.Sifre1"), true, ValidationMessages.PatientInputRequired);

    [Theory]
    [InlineData("1234567890", "Gizli.Sifre1")]
    [InlineData("123456789012", "Gizli.Sifre1")]
    [InlineData(ValidNationalId, "")]
    [InlineData(ValidNationalId, null)]
    public void PatientSignIn_InvalidInput_GetsOneFixedMessage(string nationalId, string? password) =>
        AssertResult(new PatientSignInInputValidator(), PatientSignInInput.Create(nationalId, password), false, ValidationMessages.PatientInputRequired);

    [Theory]
    [InlineData("", "Gizli.Sifre1", false)]
    [InlineData("  ", "Gizli.Sifre1", false)]
    [InlineData("Dr. Ada Örnek", "", false)]
    [InlineData("Dr. Ada Örnek", "x", true)]
    public void DoctorSignIn_RequiresNameAndPassword(string name, string password, bool valid) =>
        AssertResult(new DoctorSignInInputValidator(), DoctorSignInInput.Create(name, password), valid, ValidationMessages.DoctorInputRequired);

    [Theory]
    [InlineData(9, 0, true)] // first slot
    [InlineData(16, 30, true)] // last slot, ends at 17:00
    [InlineData(17, 0, false)] // would end after closing time
    [InlineData(8, 30, false)] // before opening time
    [InlineData(11, 15, false)] // not on a slot boundary
    public void Booking_SlotBoundaries(int hour, int minute, bool valid)
    {
        var tomorrow = Now.Date.AddDays(1);

        AssertResult(new BookingInputValidator(), new BookingInput(1, tomorrow.AddHours(hour).AddMinutes(minute), Now), valid, ValidationMessages.InvalidSlot);
    }

    [Fact]
    public void Booking_OneSecondIntoASlot_IsRejected() =>
        AssertResult(new BookingInputValidator(), new BookingInput(1, Now.Date.AddDays(1).AddHours(10).AddSeconds(1), Now), false, ValidationMessages.InvalidSlot);

    [Fact]
    public void Booking_Weekend_IsRejected() =>
        AssertResult(new BookingInputValidator(), new BookingInput(1, new DateTime(2026, 3, 7, 10, 0, 0), Now), false, ValidationMessages.InvalidSlot);

    [Fact]
    public void Booking_SlotStartingNow_IsInThePast()
    {
        var now = new DateTime(2026, 3, 4, 10, 30, 0);

        AssertResult(new BookingInputValidator(), new BookingInput(1, now, now), false, ValidationMessages.PastSlot);
        AssertResult(new BookingInputValidator(), new BookingInput(1, now, now.AddTicks(-1)), true, ValidationMessages.PastSlot);
    }

    [Fact]
    public void Booking_HorizonIsNinetyDaysInclusive()
    {
        var day90 = Now.Date.AddDays(AppointmentRules.MaxDaysAhead);
        var day91 = day90.AddDays(1);
        Assert.True(AppointmentRules.IsWorkingDay(day90) && AppointmentRules.IsWorkingDay(day91));

        AssertResult(new BookingInputValidator(), new BookingInput(1, day90.AddHours(16.5), Now), true, ValidationMessages.TooFarAhead);
        AssertResult(new BookingInputValidator(), new BookingInput(1, day91.AddHours(9), Now), false, ValidationMessages.TooFarAhead);
    }

    [Fact]
    public void Booking_HorizonCountsCalendarDaysFromTheCurrentDate()
    {
        // Late in the evening the window still ends on day 90 of today's date, not 90 x 24 hours later.
        var lateNow = Now.Date.AddHours(23).AddMinutes(59);
        var day90 = Now.Date.AddDays(AppointmentRules.MaxDaysAhead);

        AssertResult(new BookingInputValidator(), new BookingInput(1, day90.AddHours(9), lateNow), true, ValidationMessages.TooFarAhead);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Booking_NoDoctor_IsReportedBeforeTheTime(int doctorId) =>
        AssertResult(new BookingInputValidator(), new BookingInput(doctorId, Now.AddYears(-1), Now), false, ValidationMessages.DoctorRequired);

    [Fact]
    public void Cancellation_AlreadyCancelled_IsReportedFirst() =>
        AssertResult(new CancellationInputValidator(), new CancellationInput(AppointmentStatus.Cancelled, Now.AddDays(-1), Now), false, ValidationMessages.AlreadyCancelled);

    [Fact]
    public void Cancellation_IsAllowedUntilTheAppointmentStarts()
    {
        var start = Now.Date.AddHours(11);

        AssertResult(new CancellationInputValidator(), new CancellationInput(AppointmentStatus.Active, start, start.AddTicks(-1)), true, ValidationMessages.StartedCannotCancel);
        AssertResult(new CancellationInputValidator(), new CancellationInput(AppointmentStatus.Active, start, start), false, ValidationMessages.StartedCannotCancel);
        AssertResult(new CancellationInputValidator(), new CancellationInput(AppointmentStatus.Active, start, start.AddMinutes(5)), false, ValidationMessages.StartedCannotCancel);
    }

    [Fact]
    public void Messages_AreTheOriginalTurkishTexts()
    {
        Assert.Equal("Şifre en az 8, en fazla 64 karakter olmalıdır.", ValidationMessages.PasswordInvalid);
        Assert.Equal("Lütfen geçerli TC ve şifre giriniz.", ValidationMessages.PatientInputRequired);
        Assert.Equal("Randevu en fazla 90 gün sonrası için alınabilir.", ValidationMessages.TooFarAhead);
    }

    [Fact]
    public void ValidateOrThrow_RaisesTheFirstMessageWithoutTheEnteredValues()
    {
        var input = RegistrationInput.Create("Deniz Örnek", "10000000451", "Gizli.Sifre1");

        var error = Assert.Throws<BusinessRuleException>(() => new RegistrationInputValidator().ValidateOrThrow(input));

        Assert.Equal(ValidationMessages.NationalIdInvalid, error.Message);
        Assert.DoesNotContain("10000000451", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Deniz", error.Message, StringComparison.Ordinal);
    }

    private static void AssertResult<T>(IValidator<T> validator, T input, bool valid, string message)
    {
        var result = validator.Validate(input);

        if (valid)
        {
            Assert.True(result.IsValid, result.IsValid ? null : result.Errors[0].ErrorMessage);
        }
        else
        {
            Assert.False(result.IsValid);
            Assert.Equal(message, result.Errors[0].ErrorMessage);
        }
    }
}
