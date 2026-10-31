# GiftOfTheGivers.Helpers

A custom helper library created for the **Gift of the Givers Foundation** web system.

## Features
- **TaxCertificateHelper**:
  - `FormatCertificateNumber(int donationId, DateTime? date)` — Generates SARS compliant Section 18A certificate numbers (e.g. `GOTG-18A-2026-00042`).
  - `CalculateTaxRelief(decimal amount, decimal rate = 0.28m)` — Computes estimated SARS income tax rebate.
  - `GetPboRegistrationDetails()` — Official PBO and NPO registration details.
  - `GenerateVerificationHash(certNumber, amount, email)` — Cryptographic anti-fraud validation token.
- **DonationMetricsHelper**:
  - `FormatZar(decimal amount)` — South African Rand currency formatting.
  - `CalculateTotal(IEnumerable<decimal>)` — Aggregates donation totals.
  - `CalculateAverage(IEnumerable<decimal>)` — Calculates donation averages.
  - `GenerateDonorReference(string donorName, int id)` — Standard donor reference generation.
  - `GetDonationTier(decimal amount)` — Donor recognition tier classification.

## Packaging & Azure Artifacts
Packaged as `GiftOfTheGivers.Helpers.1.0.0.nupkg` and hosted on the Azure Artifacts NuGet feed.
