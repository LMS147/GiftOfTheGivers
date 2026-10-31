# Assignment A: automated testing

The solution includes **GiftOfTheGivers.AssignmentTests**, a C# xUnit project supported by Visual Studio Test Explorer. Existing regression tests remain in GiftOfTheGivers.Tests.

| Section | Source in GiftOfTheGivers.AssignmentTests | Verification |
| --- | --- | --- |
| A.1 | Unit/DonationUnitTests.cs | Invalid amounts (three boundary cases), decimal totals, certificate reference formatting, empty average: six executable cases. |
| A.2 | Integration/DonationFlowIntegrationTests.cs | Anonymous form → review → confirmation → SQL write → actual certificate Function → shared SQL read. |
| A.3 | Performance/LoadAndStressTests.cs | Five concurrent virtual users, then twenty users; request counts, failures, latency, p95, throughput and peak outstanding requests. |
| A.4 | Selenium/SeleniumUiTests.cs | Employee login into Staff dashboard; anonymous donation through Details → Review → Confirmation and SQL verification. |

## A.1: Visual Studio unit tests

1. Pull this branch and open GiftOfTheGivers.sln in Visual Studio with .NET 9 support, the .NET 9 SDK and ASP.NET workload installed.
2. Build the solution. NuGet restores xUnit, the test adapter and Selenium.
3. Open **Test → Test Explorer**. Group by **Traits**, select the **Unit** tests under GiftOfTheGivers.AssignmentTests, and run the six cases.
4. Capture Test Explorer showing the project, test names and passing results. Unit tests require no SQL Server, browser or Azure account.

```powershell
dotnet test GiftOfTheGivers.AssignmentTests/GiftOfTheGivers.AssignmentTests.csproj --filter "Category=Unit" --logger "trx;LogFileName=assignment-unit.trx" --results-directory TestResults/Assignment
```

Representative assertions from the actual source:

```csharp
Assert.Contains(errors, error => error.MemberNames.Contains(nameof(DonationInput.Amount)));
Assert.Equal(150.00m, DonationMetricsHelper.CalculateTotal(new[] { 50m, 99.99m, 0.01m }));
Assert.Equal("GOTG-18A-2026-00042",
    TaxCertificateHelper.FormatCertificateNumber(42, new DateTime(2026, 1, 2)));
```

The boundary test supplies zero, a negative amount and an amount above the maximum, checking the real DonationInput attributes. Decimal totals verify cents are preserved; the certificate test fixes its date to avoid clock dependence.

## Prepare A.2–A.4 locally

Install **SQL Server Express LocalDB** (Visual Studio Installer → Individual components), **Azure Functions Core Tools v4**, **Node.js**, **Azurite** and **Google Chrome**. Check `dotnet --version`, `func --version`, `azurite --version` and `sqllocaldb info`. Restart Visual Studio after installing tools so it receives the new PATH.

```powershell
npm install -g azurite
```

Core Tools installation: https://learn.microsoft.com/azure/azure-functions/functions-run-local

On Windows the fixture defaults to `(localdb)\MSSQLLocalDB` with Windows authentication. It creates a unique disposable test database, runs real EF migrations, seeds an employee and project, publishes the web and Functions applications, and starts them on random local ports. It deletes its database afterwards and starts Azurite if port 10000 is unused. No Azure database key or subscription is needed. Local Core Tools does not enforce deployed Function keys, so deployed authentication is outside these tests.

The fixture accepts **local SQL Server only**. Never use your deployed Azure connection string. For local SQL Server instead of LocalDB, set `GOTG_TEST_SQL` to a localhost connection with create/drop database permissions before launching Visual Studio. CI provisions a disposable SQL Server container automatically. Credentials are not written to evidence files.

Selenium Manager resolves ChromeDriver for your installed Chrome (internet required on first use).

| Optional environment variable | Purpose |
| --- | --- |
| GOTG_SELENIUM_HEADLESS=false | Show Chrome during execution; default is headless. |
| GOTG_FUNC_PATH / GOTG_AZURITE_PATH | Full tool executable paths when absent from PATH. |
| GOTG_CHROME_BINARY / GOTG_CHROMEDRIVER | Specific Chrome binary / driver executable. |
| GOTG_EVIDENCE_DIR | Absolute output directory for logs, JSON and PNGs. |
| GOTG_REPO_ROOT | Solution directory if the runner cannot find it. |

Do not run multiple system test processes against the same evidence directory.

## A.2: integration test

Run the **Integration** trait in Test Explorer. The test uses real HTTP requests and preserves antiforgery and anonymous certificate cookies. It follows the implemented app: web writes SQL first, then the certificate Function reads the record. A fallback certificate fails this test.

```csharp
var result = await DonationFlow.SubmitAndVerifyAsync(environment);
// Assertions inside the flow:
Assert.Equal(175.25m, row.Amount);
Assert.Null(row.DonorId);
Assert.True(json.GetProperty("isCloudGenerated").GetBoolean());
Assert.Equal(row.ReferenceNumber, json.GetProperty("certificateNumber").GetString());
```

Submit the source, passing Test Explorer screenshot, and integration-result.json with actual donation ID, amount, reference and execution time.

## A.3: load and stress test

Run the **Load** trait. Performance/load-settings.json configures five warm-up requests, ten seconds at five users, ten seconds at twenty users, 100 ms think time and a 15-second request timeout. Users request the home page, projects list and donation form. Each user waits for its response before the next request; virtual users and peak simultaneous outstanding requests are reported separately.

```json
"phases": [
  { "name": "load", "virtualUsers": 5, "durationSeconds": 10 },
  { "name": "stress", "virtualUsers": 20, "durationSeconds": 10 }
]
```

Acceptance targets: at least 95% successful responses in both phases and five-user p95 latency <= 3000 ms. These are assignment targets, not a production service guarantee. Twenty users demonstrate increased pressure; this does not establish the breaking point or Azure capacity. Failures remain in the report and fail the test.

Submit the configuration, load-stress-result.json, load-stress-samples.csv and a screenshot of the measured results. Only the fixture's isolated local host is tested.

## A.4: Selenium tests

Run the **Selenium** trait. Both scenarios use real Chrome through the C# Selenium WebDriver package:

```csharp
browser.Navigate().GoToUrl(environment.WebUrl + "/Identity/Account/Login");
browser.FindElement(By.Id("Input_Email")).SendKeys(AssignmentEnvironment.EmployeeEmail);
browser.FindElement(By.Id("Input_Password")).SendKeys(AssignmentEnvironment.EmployeePassword);
browser.FindElement(By.CssSelector("#account button[type='submit']")).Click();
Wait(browser).Until(driver => new Uri(driver.Url).AbsolutePath.StartsWith("/Staff"));
```

The employee exists only in the disposable database. The second scenario fills the anonymous form, reviews ZAR 250.50, confirms, checks the certificate download link and verifies an ownerless SQL record. No real payment is processed by this application flow.

Browser PNGs capture the form, review, confirmation and employee dashboard. Failures retain an additional screenshot. Submit both test methods, passing execution results and browser screenshots.

## All system tests and evidence

```powershell
dotnet test GiftOfTheGivers.AssignmentTests/GiftOfTheGivers.AssignmentTests.csproj -c Release --filter "Category!=Unit" --logger "trx;LogFileName=assignment-system.trx" --results-directory TestResults/Assignment
```

System logs/JSON/PNGs are written to TestResults/Assignment/<timestamp-id>/ unless GOTG_EVIDENCE_DIR is set. TRX files go to the specified results directory. Preserve both.

GitHub Actions **.NET checks → assignment-system-tests** runs the same real SQL/Functions/Chrome tests and uploads **assignment-a1-a4-evidence** with:

- Raw unit/system TRX, integration JSON, performance data and application/Functions logs.
- Actual Selenium browser screenshots.
- evidence-index.html and A1-unit-results.png through A4-selenium-results.png rendered from real TRX/JSON.

These report screenshots are labelled **CI evidence**, not Visual Studio Test Explorer screenshots. If your lecturer requires Visual Studio screenshots, run locally and capture Test Explorer. Do not relabel CI images.

Optional local evidence rendering (Python 3 and Node.js):

```powershell
python scripts/assignment-evidence.py TestResults/Assignment
npm install --prefix .evidence-tools --no-package-lock playwright@1.56.1
.evidence-tools/node_modules/.bin/playwright install chromium
$env:PLAYWRIGHT_MODULE = (Resolve-Path .evidence-tools/node_modules/playwright).Path
node scripts/capture-assignment-evidence.cjs TestResults/Assignment
```

TestResults and .evidence-tools are excluded from git. Include relevant source, screenshots, environment and measured results in your assignment submission.
