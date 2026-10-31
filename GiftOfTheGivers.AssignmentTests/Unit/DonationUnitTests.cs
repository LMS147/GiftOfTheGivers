using System.ComponentModel.DataAnnotations;
using GiftOfTheGivers.Helpers;
using GiftOfTheGivers.Helpers.Models.ViewModels;
using Xunit;
namespace GiftOfTheGivers.AssignmentTests.Unit;

[Trait("Category", "Unit")]
public class DonationUnitTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000001)]
    public void DonationValidationRejectsAmountsOutsideTheAllowedRange(int amount)
    {
        var donation = new DonationInput { DonorName = "Test Donor", Email = "donor@example.test", Amount = amount, Currency = "ZAR" };
        var errors = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(donation, new ValidationContext(donation), errors, validateAllProperties: true);
        Assert.False(valid);
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(DonationInput.Amount)));
    }

    [Fact]
    public void TotalCalculationPreservesCentsAcrossMultipleDonations()
    {
        var total = DonationMetricsHelper.CalculateTotal(new[] { 50m, 99.99m, 0.01m });
        Assert.Equal(150.00m, total);
    }

    [Fact]
    public void CertificateReferenceUsesTheDonationYearAndPaddedRecordId()
    {
        var reference = TaxCertificateHelper.FormatCertificateNumber(42, new DateTime(2026, 1, 2));
        Assert.Equal("GOTG-18A-2026-00042", reference);
    }

    [Fact]
    public void AverageForNoDonationsIsZeroInsteadOfThrowing()
    {
        Assert.Equal(0m, DonationMetricsHelper.CalculateAverage(Array.Empty<decimal>()));
    }
}
