# Gift of the Givers — student prototype

ASP.NET Core MVC (.NET 9), Razor/Bootstrap, ASP.NET Core Identity, EF Core and SQL Server. This student project is not affiliated with the real foundation. Donation submissions are demonstration records: no payment is processed, recurring billing is not implemented, and printable receipts are not valid tax certificates.

## Projects

- `GiftOfTheGivers.csproj`: public site, donation and volunteer forms, donor history, staff and volunteer areas.
- `GiftOfTheGivers.Helpers`: models, database context, SQL migrations, seed roles/data, shared calculations. Packs as NuGet version 1.0.1.
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
- No schema tables were added. The migration snapshots now reference the actual shared-library model namespaces; migration drift is no longer hidden globally.

## Endpoint examples

Use a trusted server tool with an `x-functions-key` header:

- `GET /api/home/summary`
- `GET /api/donations`, `GET /api/donations/{id}`, `POST /api/donations`, `DELETE /api/donations/{id}`
- `GET /api/volunteers`, `GET /api/volunteers/{id}`, `POST /api/volunteers`, `PUT /api/volunteers/{id}/status`, `DELETE /api/volunteers/{id}`
- `POST /api/tax-certificate` with `{ "donationId": 1 }` (JSON only; unknown IDs return 404)
- `POST /api/log-update` with `{ "updateId": 1 }`

## Shared helper package / Azure Artifacts

The default build uses a project reference, so a private feed is not needed. To demonstrate consuming the package, publish version 1.0.1 to your own Azure Artifacts feed, authenticate using the Azure Artifacts Credential Provider, and restore/build with `UseHelperPackage=true`. Configure the feed locally without committing a PAT.

```bash
dotnet pack GiftOfTheGivers.Helpers/GiftOfTheGivers.Helpers.csproj -c Release
dotnet nuget push GiftOfTheGivers.Helpers/nupkg/GiftOfTheGivers.Helpers.1.0.1.nupkg --source YOUR_FEED_URL --api-key az
dotnet restore GiftOfTheGivers.csproj -p:UseHelperPackage=true --source https://api.nuget.org/v3/index.json --source YOUR_FEED_URL
dotnet build GiftOfTheGivers.csproj -c Release -p:UseHelperPackage=true --no-restore
```

Publishing a package or deploying Azure resources still requires your actual Azure configuration. No private feed, cloud resources or credentials are created by this code change.

## Automated checks

`.github/workflows/dotnet.yml` builds all projects and runs the regression tests on pushes and pull requests. Unit tests do not require SQL Server or Azure; the migration consistency test builds the SQL model without connecting. Real SQL migration/application startup and successful Blob uploads still require an integration check against your configured services.
