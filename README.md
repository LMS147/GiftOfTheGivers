# Gift of the Givers — student prototype

ASP.NET Core MVC (.NET 9), Razor/Bootstrap, ASP.NET Core Identity, EF Core and SQL Server. This student project is not affiliated with the real foundation. Donation submissions are demonstration records: no payment is processed, recurring billing is not implemented, and printable receipts are not valid tax certificates.

## Projects

- `GiftOfTheGivers.csproj`: public site, donation and volunteer forms, donor history, staff and volunteer areas.
- `GiftOfTheGivers.Helpers`: models, database context, SQL migrations, seed roles/data, shared calculations. Packs as NuGet version 1.1.0.
- `GiftOfTheGivers.Functions`: isolated Azure Functions for trusted CRUD, database-backed receipt generation and durable project-update audit blobs.
- `GiftOfTheGivers.Tests`: access-control, Function authorization, audit-failure and migration-snapshot regression tests.

## Run locally

Install .NET 9 SDK and SQL Server/LocalDB. Set `ConnectionStrings:DefaultConnection` in development configuration or user secrets. The web app applies EF migrations at startup. Both applications must point to the SAME database.

```bash
dotnet restore GiftOfTheGivers.sln
dotnet build GiftOfTheGivers.sln --configuration Release
dotnet test GiftOfTheGivers.Tests/GiftOfTheGivers.Tests.csproj --configuration Release
dotnet run --project GiftOfTheGivers.csproj
```

The local development configuration enables demo seeding. Demo users (`staff@gotg.org`, `volunteer@gotg.org`, `donor@gotg.org`) use `Password123!`. Seeding demo accounts/data requires BOTH Development environment and `SeedDemoData=true`. Other environments only seed roles. Existing demo accounts from earlier deployments are NOT removed by this change: remove or secure those accounts in the deployed database before using real information.

Register as Donor or Volunteer; public registration cannot create Staff. Staff administration is available at `/Staff`, volunteer self-service at `/Volunteer`. Donation management pages (`/Donations`, Details, Delete) require Staff.

## Azure Functions configuration

Leaving `AzureFunctions:BaseUrl` empty runs the website in local database mode. When Functions are deployed, configure the web app settings:

| Setting | Value |
|---|---|
| `AzureFunctions__BaseUrl` | `https://YOUR-FUNCTION-APP.azurewebsites.net` (no `/api` suffix) |
| `AzureFunctions__FunctionKey` | A Function App host key, stored as an application secret |
| `ConnectionStrings__DefaultConnection` | The shared SQL Server connection string |

An optional `AzureFunctions__TaxCertificateUrl` can override only the certificate endpoint. Do not embed a function key in a URL or browser script; the web app sends it in `x-functions-key` server-side. Remove any obsolete placeholder endpoint settings from earlier deployments.

For local Functions, copy `GiftOfTheGivers.Functions/local.settings.example.json` to `local.settings.json` (ignored by git), provide the shared database connection, and run Azure Functions Core Tools. Azurite is required when using `UseDevelopmentStorage=true`.

```bash
cd GiftOfTheGivers.Functions
func start --enableAuth
```

Core Tools normally bypasses key checks locally unless authorization is enabled. In Azure all HTTP Functions now require a function key. These endpoints are a TRUSTED server API: do not share the key with browser/mobile clients or end users. If direct end-user API access is added later, implement per-user token authorization first.

Functions application settings:

| Setting | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | Same SQL database as the web app |
| `AzureWebJobsStorage` | Azure Functions runtime storage |
| `AuditStorageConnectionString` | Azure Blob Storage connection for private `project-update-audits` container |
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` |

Publish the Functions project and web project separately. Start the web app once to apply migrations before invoking the database-backed Functions.

## Write and access behavior

- Web forms save directly to SQL once; a failed cloud call never triggers a second submission write. API CRUD remains available for trusted server integration.
- Home metrics and staff donation reads can use Functions with a database fallback. Prototype receipt generation uses the saved donation ID and values; caller-supplied donor names/amounts are ignored.
- Account-owned receipts and confirmation data are accessible only to the owner or Staff. Guest submissions grant their submitting browser a protected, HttpOnly cookie valid for 24 hours. Other browsers and guessed IDs cannot read the confirmation. Keep ASP.NET Core Data Protection keys persistent/shared across restarts and instances to preserve these grants.
- Project updates are saved to SQL. If Functions are configured, staff posting calls `/api/log-update` with the saved update ID. The Function reads the saved update and uploads a private JSON blob. Missing/failed storage returns 503 and does not claim success. Staff can retry from the Updates page; stable blob names prevent duplicate audit objects. There is no automatic retry queue.
- The experience upgrade adds Notifications and StaffAuditEntries tables, project categories/targets/status/images, and optional project/submission keys on donations. Back up your database before upgrading; startup applies migration 20261031090000_ExperienceUpgrade. Migration drift remains checked.

## Endpoint examples

Use a trusted server tool with an `x-functions-key` header:

- `GET /api/home/summary`
- `GET /api/donations`, `GET /api/donations/{id}`, `POST /api/donations`, `DELETE /api/donations/{id}`
- `GET /api/volunteers`, `GET /api/volunteers/{id}`, `POST /api/volunteers`, `PUT /api/volunteers/{id}/status`, `DELETE /api/volunteers/{id}`
- `POST /api/tax-certificate` with `{ "donationId": 1 }` (JSON only; unknown IDs return 404)
- `POST /api/log-update` with `{ "updateId": 1 }`

## Shared helper package / Azure Artifacts

The default build uses a project reference, so a private feed is not needed. To demonstrate consuming the package, publish version 1.1.0 to your own Azure Artifacts feed, authenticate using the Azure Artifacts Credential Provider, and restore/build with `UseHelperPackage=true`. Configure the feed locally without committing a PAT.

```bash
dotnet pack GiftOfTheGivers.Helpers/GiftOfTheGivers.Helpers.csproj -c Release
dotnet nuget push GiftOfTheGivers.Helpers/nupkg/GiftOfTheGivers.Helpers.1.1.0.nupkg --source YOUR_FEED_URL --api-key az
dotnet restore GiftOfTheGivers.csproj -p:UseHelperPackage=true --source https://api.nuget.org/v3/index.json --source YOUR_FEED_URL
dotnet build GiftOfTheGivers.csproj -c Release -p:UseHelperPackage=true --no-restore
```

Publishing a package or deploying Azure resources still requires your actual Azure configuration. No private feed, cloud resources or credentials are created by this code change.

## Automated checks

`.github/workflows/dotnet.yml` builds all projects and runs the regression tests on pushes and pull requests. Unit tests do not require SQL Server or Azure; the migration consistency test builds the SQL model without connecting. Real SQL migration/application startup and successful Blob uploads still require an integration check against your configured services.


## Donor and staff experience

- `/Projects`: searchable/filterable project cards with local illustrations, project details, recent field updates, and explicitly labelled ZAR demo progress. Staff manage projects at `/Staff/Projects`; closing preserves history and stops new contributions.
- `/Donations/Create`: Details → Review → Confirmation, with a protected 30-minute review token and unique submission key. Replaying a confirmed review returns the same record instead of creating a duplicate. Monthly remains a preference; payments stay in demo mode.
- `/Donations/MyDonations`: paginated history, totals/averages grouped by currency, project links and protected PDF receipt downloads. Guests retain 24-hour browser access to their own receipts.
- `/Staff`: pending applications and active projects have actionable summary cards. Volunteer and donation tables support server-side search, filters and pagination.
- `/Notifications`: account-only in-app confirmations and decisions. `/Staff/Audit` provides read-only staff history for project edits, application decisions, field updates, deleted donation records and email retries.
- `/Staff/Diagnostics`: configuration state and email delivery queue. Technical payloads have been removed from donor receipts. Configuration indicators do not assert service health.
- Mobile navigation, skip links, focus outlines, labelled filters, reduced motion, loading feedback, duplicate-submit prevention and useful empty states are included.

### Email notification setup

Email is disabled by default, while in-app notifications and the durable queue work immediately. Configure these through user secrets or environment variables:

| Setting | Example / purpose |
|---|---|
| `Email__Enabled` | `true` after testing |
| `Email__Host` | Your SMTP hostname |
| `Email__Port` | `587` for STARTTLS |
| `Email__UseTls` | `true` |
| `Email__Username` / `Email__Password` | SMTP credentials; never commit them |
| `Email__FromAddress` | A sender address verified with your provider |
| `Email__FromName` | Your portal's sender name |

The worker claims up to 20 queued messages every 30 seconds, uses an atomic database claim to avoid two workers processing one attempt, retries failures with backoff and stops after five attempts. Staff can requeue exhausted messages from Diagnostics. SMTP delivery is at least once: a process crash after SMTP accepts an email but before the database acknowledgement can result in a duplicate. Use STARTTLS on port 587 (implicit TLS/465 is not supported by this SMTP adapter). No emails are sent until explicitly enabled and configured.

The PDF receipt uses a standard built-in font; characters outside printable ASCII are replaced with `?`. The HTML receipt retains full names and remains printable through the browser.

All targets and contribution totals are demonstration figures. Do not enable live payment collection for this student prototype; no payment gateway is configured.


### Donor workspace

Donor accounts land at `/Donor` after a default login or registration. An explicit return URL is preserved. The overview includes currency-specific recorded totals, six-month activity, recent contributions, supported projects, notifications and the latest PDF receipt. `/Donations/MyDonations` is the separate contribution-history screen, with currency, reference/project and date filters.

The shared dashboard layout now distinguishes Staff, Volunteer and Donor navigation. The two selected yellow notices were removed from the contribution form and HTML receipt; review-step payment information and the underlying demo payment behavior are unchanged. This update does not require a schema migration.


## Create or recover your production Staff account

Production deployments do not seed the published demo accounts. Use deployment settings to create a Staff account through ASP.NET Core Identity; do not insert passwords or hashes manually into SQL.

After deploying this version, open **Azure App Service → Settings → Environment variables → App settings** and add:

| Setting | Value |
|---|---|
| `StaffBootstrap__Enabled` | `true` |
| `StaffBootstrap__Email` | The email you want to use for Staff login |
| `StaffBootstrap__Password` | A new private password with at least 12 characters, uppercase, lowercase, a digit and a symbol |
| `StaffBootstrap__ResetPassword` | `false` for a new account; `true` only when intentionally recovering an existing Staff account |

Apply the settings and restart the application. The startup log should contain **Staff bootstrap completed: Created** (or Promoted/PasswordReset). Sign in at `/Identity/Account/Login` with that email and password. Staff login routes to `/Staff/Dashboard`.

After successfully signing in, set `StaffBootstrap__Enabled=false`, set `StaffBootstrap__ResetPassword=false`, and remove `StaffBootstrap__Password` from the App Service settings. The account and its password hash remain in the database. Keep the app environment as Production; no demo seeding is required.

Behavior and troubleshooting:

- Disabled or missing setup settings leave accounts unchanged. Credentials are never committed or logged.
- An existing Staff account keeps its password by default. `ExistingStaffPasswordMismatch` means the supplied password does not match: use the correct password or explicitly set ResetPassword=true to recover that Staff account. Explicit recovery also clears lockout.
- An existing Donor/Volunteer account is promoted only if the supplied password matches its current password. `ExistingAccountPasswordMismatch` refuses promotion and does not reset that account; use the correct current password or a new, unused email address.
- `WeakPassword` or `InvalidEmail` means the settings need correction. Identity validation failures report error codes without credentials. Setup failures are logged without taking the website offline.
- Creation, role assignment and audit changes use one transaction on SQL Server. No schema migration is added by this setup feature; the existing Identity/audit schema is used.
- This is an operator-controlled setup/recovery mechanism, not a public Staff registration page. Disable it and remove its password setting after setup to avoid reapplying recovery settings on future restarts.
