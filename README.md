# Gift of the Givers — ASP.NET Core MVC (**.NET 9**) + Azure SQL + App Service

A student prototype for **Section 3.2 — Developing a Professional Prototype**.
Runs locally on your machine and deploys to **Azure App Service** with an
**Azure SQL Database** behind it. Features **role-based dashboards**: Staff and
Volunteers each get their own area with a dedicated sidebar — completely
separate from the public landing page.

## Tech stack

| Layer | Choice |
|---|---|
| Framework | ASP.NET Core MVC on **.NET 9** |
| UI | **Bootstrap 5.3** + Bootstrap Icons + self-hosted Inter/Plus Jakarta Sans |
| Auth | **ASP.NET Identity** with three roles: `Staff`, `Volunteer`, `Donor` |
| Data | **EF Core + SQL Server** everywhere — Azure SQL Database in production, **LocalDB** for local development |

### Database context (one provider, one config)

`Program.cs` uses the standard template configuration:

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    // ...
});
```

The connection string comes from configuration:
- **Local dev:** `appsettings.Development.json` → LocalDB (`Server=(localdb)\MSSQLLocalDB;Database=GiftOfTheGiversDb;Trusted_Connection=True;...`)
- **Azure:** the `DefaultConnection` connection string you set in App Service → Configuration → Connection strings (type `SQLAzure`) **overrides** `appsettings.json` automatically

> ⚠️ The in-browser preview in this workspace runs an earlier build because this
> sandbox has no SQL Server — on your machine (LocalDB or Azure SQL) the current
> code runs normally.

### Roles & dashboards

| Role | How accounts are created | Where they land after login | Dashboard |
|---|---|---|---|
| **Staff** | Seed data / admin (not via public register) | `/Staff/Dashboard` | Overview, Volunteer applications (approve/decline), Post field update, Donations |
| **Volunteer** | Public register (choose "Volunteer") | `/Volunteer/Dashboard` | Overview (application status timeline), My application, Relief updates |
| **Donor** | Public register (choose "Donor") | wherever they were going | No dashboard — public site + "My donations" |

Each dashboard uses its own **sidebar layout** (`_DashboardLayout.cshtml`) with
role-relevant links only. The public landing page keeps the top navbar — the
sidebar **never** appears on public pages. Anonymous users hitting `/Staff` or
`/Volunteer` are redirected to the login page; Donors/Volunteers reaching Staff
pages get **Access Denied**.

## Run it on your machine

1. Install the **.NET 9 SDK**: <https://dotnet.microsoft.com/download/dotnet/9.0>
2. **Database:** the app uses SQL Server. For local development it defaults to
   **LocalDB** (`appsettings.Development.json`), which ships with Visual Studio.
   If you don't have LocalDB, either:
   - install it (Visual Studio Installer → Individual components → "SQL Server
     Express LocalDB"), or
   - point the connection string at your Azure SQL database (and allow your IP
     in the server firewall).
3. In this folder (`GiftOfTheGivers`), run:

```bash
dotnet run
```

4. Open the printed URL (`http://localhost:5182` or `https://localhost:7182`).

**First run:** `MigrateAsync()` creates the `GiftOfTheGiversDb` database and all
tables in LocalDB, then the seeder adds:

- Roles: `Staff`, `Volunteer`, `Donor`
- Demo accounts (all password `Password123!`):
  - `staff@gotg.org` → **Staff**
  - `volunteer@gotg.org` → **Volunteer** (has an approved application)
  - `donor@gotg.org` → **Donor**
- Three relief projects, two volunteer applications (one pending: "Lerato Mokoena"),
  and two field updates.

> Want a fresh local database? In SSMS (or Visual Studio's SQL Server Object
> Explorer) delete the `GiftOfTheGiversDb` database, or run
> `dotnet ef database drop` from the project folder — it will be recreated and
> reseeded on the next `dotnet run`.

## Page map

**Public site (top navbar):** Home · About · Donate · Volunteer · Contact ·
My donations (logged in) · Staff dashboard (Staff only)

**Staff area — `/Staff/...` (sidebar):**
| Page | URL | Purpose |
|---|---|---|
| Overview | `/Staff` | Stats + recent applications, donations, updates |
| Volunteer applications | `/Staff/Volunteers` | Full list, **Approve / Decline** buttons |
| Post field update | `/Staff/Updates` | Post updates that appear on the public home page |
| Donations | `/Staff/Donations` | All donations + ZAR total |

**Volunteer area — `/Volunteer/...` (sidebar):**
| Page | URL | Purpose |
|---|---|---|
| Overview | `/Volunteer` | Application status timeline + projects + updates |
| My application | `/Volunteer/MyApplication` | Full application details + status |
| Relief updates | `/Volunteer/Updates` | Updates posted by staff |

## Key concepts to be able to explain

- **Role-based routing after login**: `Login.cshtml.cs` checks the user's role
  with `UserManager.IsInRoleAsync` and redirects Staff → `/Staff`,
  Volunteer → `/Volunteer`. Registration asks for account type
  (`Input.Role`) and routes the same way.
- **Areas** give the dashboards their own structure:
  `Areas/Staff/{Controllers,Views}` and `Areas/Volunteer/{Controllers,Views}`,
  registered with `MapAreaControllerRoute` in `Program.cs`.
- **`[Authorize(Roles = "Staff")]`** on the whole `StaffController` / `VolunteerDashboardController`
  (plus `[Area("...")]`) — that's what protects the dashboards.
- **Sidebar vs landing separation**: `_DashboardLayout.cshtml` + `_DashboardSidebar.cshtml`
  are only used by area views (via their `_ViewStart.cshtml`); public pages use
  `_Layout.cshtml`. Mobile uses a Bootstrap **offcanvas** sidebar.
- **`[AllowAnonymous]`** on Donate/Volunteer forms vs `[Authorize]` on My donations.
- **`MigrateAsync()`** at startup applies the EF migrations in `Migrations/`,
  creating the schema on SQL Server (Azure SQL or LocalDB).
- **Bootstrap grid**: `container` → `row` → `col-*` makes everything responsive.
- Recurring donation = `IsRecurring` flag; currency = string; tax certificate =
  printable on-screen page (`@media print` CSS).

## Project structure

```
GiftOfTheGivers/
├── Areas/
│   ├── Staff/                 # Staff dashboard (sidebar layout)
│   │   ├── Controllers/StaffController.cs
│   │   └── Views/Staff/       # Dashboard, Volunteers, Updates, Donations
│   ├── Volunteer/             # Volunteer dashboard (sidebar layout)
│   │   ├── Controllers/VolunteerDashboardController.cs
│   │   └── Views/VolunteerDashboard/  # Dashboard, MyApplication, Updates
│   └── Identity/Pages/...     # Login/Register (role routing lives here)
├── Controllers/               # Home, Donations, Volunteers (public site)
├── Models/ (+ ViewModels)
├── Views/
│   ├── Shared/_Layout.cshtml            # public top-navbar layout
│   ├── Shared/_DashboardLayout.cshtml   # dashboard shell (sidebar + topbar)
│   ├── Shared/_DashboardSidebar.cshtml  # role-conditional sidebar links
│   └── Home/ Donations/ Volunteers/
├── Data/                     # ApplicationDbContext, DbSeeder
├── wwwroot/                  # css, js, images, fonts, bootstrap, icons
└── Program.cs                # services, area routes, Identity, seeding
```

---

# 🚀 Deploy to Azure (App Service + Azure SQL Database)

## Step 1 — Create the Azure SQL Database

**Portal:** search "SQL databases" → **Create** → select/create a **SQL server**
(write down the admin login + password) → under **Compute + storage** choose
**General Purpose + Serverless**, **min vCore 0.5, max 2, auto-pause 60 min**.

**CLI:**
```bash
az group create --name gotg-rg --location southafricanorth
az sql server create --name gotg-sql-<yourname> --resource-group gotg-rg \
  --location southafricanorth --admin-user gotgadmin --admin-password '<Strong!Passw0rd>'
az sql db create --resource-group gotg-rg --server gotg-sql-<yourname> \
  --name GiftOfTheGiversDb --edition GeneralPurpose --compute-model Serverless \
  --family Gen5 --capacity 2 --min-capacity 0.5 --auto-pause-delay 60
```

**Firewall (server → Networking):** enable **Allow Azure services**; add your own
IP if testing from your PC.

## Step 2 — Create the App Service

**Portal:** "App Services" → Create → Runtime stack **.NET 9**, OS **Linux**,
plan **Free F1**, region South Africa North.

**CLI:**
```bash
az appservice plan create --name gotg-plan --resource-group gotg-rg --sku F1 --is-linux
az webapp create --resource-group gotg-rg --plan gotg-plan \
  --name gotg-<yourname> --runtime "DOTNETCORE|9.0"
```

## Step 3 — Publish and deploy

**Visual Studio:** right-click project → **Publish** → Azure App Service (Linux).

**CLI:**
```bash
dotnet publish -c Release -o ./publish
cd publish && zip -r ../publish.zip . && cd ..
az webapp deploy --resource-group gotg-rg --name gotg-<yourname> --src-path publish.zip
```

## Step 4 — Point the app at Azure SQL

**App Service → Configuration → Connection strings:**

| Name | Value | Type |
|---|---|---|
| `DefaultConnection` | `Server=tcp:gotg-sql-<yourname>.database.windows.net,1433;Initial Catalog=GiftOfTheGiversDb;Persist Security Info=False;User ID=gotgadmin;Password=<your-password>;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;` | SQLAzure |

**CLI:**
```bash
az webapp config connection-string set --resource-group gotg-rg --name gotg-<yourname> \
  --settings 'DefaultConnection=Server=tcp:...' --connection-string-type SQLAzure
```

## How the database schema is created (important)

`Program.cs` (startup) does this once:

- `await context.Database.MigrateAsync()` — applies the real EF migration in
  `Migrations/InitialCreate`, which creates ALL tables (`AspNetRoles`,
  `AspNetUsers`, `Donations`, `Volunteers`, `ReliefProjects`, `ProjectUpdates`,
  ...). The seeder then adds roles, demo users and sample data. This works the
  same on Azure SQL and on LocalDB.

> ⚠️ If you ever get `Invalid object name 'AspNetRoles'` (HTTP 500.30) on Azure,
> it means the tables were never created — usually because an older version ran
> `MigrateAsync()` with **no migration files** (that only creates the empty
> `__EFMigrationsHistory` bookkeeping table, which then makes `EnsureCreated`
> skip creating the real tables). The fix is in this version: a real
> `Migrations/InitialCreate` migration ships with the project.
> If your Azure DB already has a leftover `__EFMigrationsHistory` (or any
> half-created tables), clean it once — see "Fix 500.30 step 2" below.

## Fixing HTTP 500.30 — step by step

**Step 1 — Use this updated project.** It now contains `Migrations/` (real EF
migration), `Data/DesignTimeDbContextFactory.cs` and the fixed `Program.cs` /
`Data/DbSeeder.cs`.

**Step 2 — Clean the half-created database (once).** In the Azure portal open
your SQL database → **Query editor** → run:

```sql
DROP TABLE IF EXISTS dbo.ProjectUpdates;
DROP TABLE IF EXISTS dbo.Donations;
DROP TABLE IF EXISTS dbo.Volunteers;
DROP TABLE IF EXISTS dbo.ReliefProjects;
DROP TABLE IF EXISTS dbo.AspNetRoleClaims;
DROP TABLE IF EXISTS dbo.AspNetUserClaims;
DROP TABLE IF EXISTS dbo.AspNetUserLogins;
DROP TABLE IF EXISTS dbo.AspNetUserRoles;
DROP TABLE IF EXISTS dbo.AspNetUserTokens;
DROP TABLE IF EXISTS dbo.AspNetRoles;
DROP TABLE IF EXISTS dbo.AspNetUsers;
DROP TABLE IF EXISTS dbo.__EFMigrationsHistory;
```

(If the database has never been used, you can also just **delete and recreate**
the database instead.)

**Step 3 — Republish.** Visual Studio: right-click → Publish → your App Service.
Or CLI: `dotnet publish -c Release -o ./publish`, zip it, `az webapp deploy ...`.

**Step 4 — Connection string (this is the config your screenshot shows).**
App Service → **Configuration → Connection strings → New connection string**:

| Field | Value |
|---|---|
| **Name** | `DefaultConnection` (must match EXACTLY) |
| **Value** | `Server=tcp:gotg-sql-<yourname>.database.windows.net,1433;Initial Catalog=GiftOfTheGiversDb;Persist Security Info=False;User ID=gotgadmin;Password=<your-password>;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;` |
| **Type** | `SQLAzure` |

This overrides the placeholder in `appsettings.json`. The same string can be
tested locally (in `appsettings.Development.json` with `"DatabaseProvider":
"SqlServer"`) if your PC's IP is allowed in the server firewall.

**Step 5 — Restart the App Service** (Overview → Restart), then open the site.
First request wakes the serverless DB (~30 s) and applies the migration +
seed. Watch **App Service → Log stream** — you should see the tables created and
`Application started`.

**Step 6 — Verify.** `https://gotg-<yourname>.azurewebsites.net` loads;
log in as `staff@gotg.org` / `Password123!` and you land on the staff dashboard.

### Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `Cannot open server ... requested by the login` / 40615 | Firewall: enable "Allow Azure services" on the SQL server |
| `Login failed for user` | Wrong admin login/password in the connection string |
| Site loads but pages crash | Check App Service → Log stream; connection string name must be exactly `DefaultConnection`, type SQLAzure |
| Model changes don't appear | `EnsureCreated` doesn't alter existing schemas — delete the DB once so it's recreated |

## Prototype limitations (mention in your report)

- "Recurring" stores a flag only — no payment gateway or exchange rates.
- Certificate is on-screen/print-to-PDF, not a legal SARS document.
- Demo accounts are seeded with known passwords; no email is sent.
- Schema created with `EnsureCreatedAsync()` for simplicity (production apps use EF migrations).
- Serverless Azure SQL auto-pauses when idle (first request may take ~30 s to wake).

## Reflection — write this yourself

The brief requires an honest paragraph on who did what (UI/branding, auth &
roles, donations, volunteers, testing) and a paragraph on the Azure benefits
(scalability via scale-out, 24/7 availability, centralised live data) — that
part is yours to write.

---

# SECTION A: Extending the Web App with Azure Functions (30 Marks)

## Architecture Overview

```
 [Donor / Browser]  --->  [ASP.NET Core Web App]  --->  [Azure Functions HTTP Trigger]
                                                          • GenerateTaxCertificate
                                                          • LogProjectUpdate
                                                                    │
                                                                    ▼
                                                    [GiftOfTheGivers.Helpers (.nupkg)]
```

### A.1 Function Project inside Solution (10 Marks)

The solution `GiftOfTheGivers.sln` contains the Azure Functions project:
- **Project Name:** `GiftOfTheGivers.Functions`
- **Framework:** `.NET 9 (Isolated Worker Model)`
- **Trigger Type:** HTTP Trigger with ASP.NET Core integration (`Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore`)
- **Authorization Level:** `Anonymous` (allows direct HTTP invocations from the prototype web app and Postman)

#### Key Files in Function Project:
1. `GenerateTaxCertificateFunction.cs` — Handles `GET` and `POST` at `/api/tax-certificate`.
   - Generates official SARS Section 18A tax certificate numbers (e.g. `GOTG-18A-2026-00042`).
   - Calculates 28% estimated SARS tax deductible relief.
   - Computes SHA256 anti-fraud digital verification token.
   - Formats currency in South African Rand (ZAR) via `GiftOfTheGivers.Helpers`.
   - Supports both `JSON` API response and full `HTML` certificate preview for browser visits!
2. `LogProjectUpdateFunction.cs` — Handles `POST` at `/api/log-update`.
   - Records employee field disaster updates with audit logging metadata.
3. `host.json` & `local.settings.json` — Preconfigured for local execution on port `7071`.

#### Web App Connection (`GiftOfTheGivers`):
- `Services/ITaxCertificateService.cs` & `Services/TaxCertificateService.cs`: Registered via `AddHttpClient<ITaxCertificateService, TaxCertificateService>()` in `Program.cs`.
- When a donor completes the form in `DonationsController.cs` (`[HttpPost] Create`), the controller saves the donation and invokes the Azure Function endpoint.
- In `Views/Donations/Certificate.cshtml`, a live badge displays `Cloud Verified: Generated by Azure Function (HTTP Trigger: GenerateTaxCertificate)` along with an interactive "View Azure Function Response" modal.

---

### A.2 Run and Test the Function Locally (10 Marks)

#### 1. Running in Visual Studio
1. Right-click the Solution `GiftOfTheGivers` in Solution Explorer → **Configure Startup Projects...**
2. Choose **Multiple startup projects**:
   - `GiftOfTheGivers` → **Start**
   - `GiftOfTheGivers.Functions` → **Start**
3. Press **F5** (or Debug → Start Debugging).
   - Visual Studio starts the Azure Functions runtime console at `http://localhost:7071`
   - Visual Studio launches the Web App in your browser at `https://localhost:7xxx` (or `http://localhost:5000`)

#### 2. Testing in the Web Browser (GET Request)
Open your browser and paste:
```
http://localhost:7071/api/tax-certificate?donationId=101&donorName=Sarah+Connor&amount=1500&email=sarah@gotg.org
```
- **Result:** Returns full structured JSON payload with certificate number `GOTG-18A-2026-00101`, tax relief `R 420.00`, and verification hash.
- **For HTML Certificate view in browser:** Add `&format=html` to the URL:
```
http://localhost:7071/api/tax-certificate?donationId=101&donorName=Sarah+Connor&amount=1500&email=sarah@gotg.org&format=html
```

#### 3. Testing with Postman (POST Request)
1. **Method:** `POST`
2. **URL:** `http://localhost:7071/api/tax-certificate`
3. **Headers:**
   - `Content-Type: application/json`
4. **Body (raw JSON):**
```json
{
  "donationId": 250,
  "donorName": "David Miller",
  "email": "david.miller@example.co.za",
  "amount": 5000.00,
  "currency": "ZAR",
  "projectName": "Disaster Relief Emergency"
}
```
5. **Expected Response (Status: 200 OK):**
```json
{
  "success": true,
  "certificateNumber": "GOTG-18A-2026-00250",
  "pboRegistrationNumber": "930018993",
  "npoRegistrationNumber": "028-871-NPO",
  "organizationName": "Gift of the Givers Foundation",
  "donationId": 250,
  "donorName": "David Miller",
  "email": "david.miller@example.co.za",
  "amount": 5000.00,
  "currency": "ZAR",
  "formattedAmount": "R 5,000.00",
  "estimatedTaxRelief": "R 1,400.00",
  "estimatedTaxReliefValue": 1400.00,
  "donorReference": "GOTG-REF-DAVID-00250",
  "donorTier": "Gold Supporter",
  "projectName": "Disaster Relief Emergency",
  "issueDateUtc": "2026-09-24T10:59:57Z",
  "verificationHash": "376C9503376E17BC",
  "legalNotice": "This receipt is issued in terms of Section 18A(1)(a) of the Income Tax Act 58 of 1962. The donation will be used exclusively for approved Public Benefit Activities.",
  "generatedBy": "Azure Functions (HTTP Trigger: GenerateTaxCertificate)"
}
```

---

### A.3 Deploy the Function to Azure (10 Marks)

#### Step 1: Create Azure Function App in Azure Portal
1. Search for **Function App** in the Azure Portal → click **Create**.
2. **Basics:**
   - **Subscription:** Your student subscription
   - **Resource Group:** Select your existing resource group (e.g., `rg-giftofthegivers`)
   - **Function App name:** `giftofthegivers-functions-xyz` (must be globally unique)
   - **Runtime stack:** `.NET`
   - **Version:** `9 (Isolated)`
   - **Operating System:** `Windows` or `Linux`
   - **Plan:** `Consumption (Serverless)` or `Flex Consumption`
3. Click **Review + create** → **Create**.

#### Step 2: Publish from Visual Studio
1. In Visual Studio Solution Explorer, right-click `GiftOfTheGivers.Functions` → **Publish...**
2. Target: **Azure** → click **Next**.
3. Specific target: **Azure Function App (Windows)** or **Azure Function App (Linux)** → click **Next**.
4. Sign in with your Azure student credentials.
5. Select your newly created Function App (`giftofthegivers-functions-xyz`) → click **Finish**.
6. On the publish summary screen, click the **Publish** button in the top right.
7. Visual Studio builds and uploads the package. Output window will show:
   ```
   Publish Succeeded.
   Web App was published successfully https://giftofthegivers-functions-xyz.azurewebsites.net
   ```

#### Step 3: Get the Live URL & Configure Web App
1. Your live Function URL is:
   ```
   https://<your-function-name>.azurewebsites.net/api/tax-certificate
   ```
2. Test it directly in your browser:
   ```
   https://<your-function-name>.azurewebsites.net/api/tax-certificate?donationId=1&donorName=Live+Test&amount=500
   ```
3. Update your Web App App Service:
   - Go to Azure Portal → **App Services** → `GiftOfTheGivers` → **Configuration** (or Environment variables).
   - Add new Application Setting:
     - **Name:** `AzureFunctions__TaxCertificateUrl`
     - **Value:** `https://<your-function-name>.azurewebsites.net/api/tax-certificate`
   - Click **Save** and **Restart**.

---

# SECTION D: Package Hosting with Azure Artifacts (20 Marks)

## Architecture Overview

```
 [GiftOfTheGivers.Helpers Project] 
            │
      dotnet pack
            │
            ▼
 [GiftOfTheGivers.Helpers.1.0.0.nupkg]
            │
      dotnet nuget push
            │
            ▼
 [Azure DevOps Artifacts Feed: GiftOfTheGiversFeed]
            │
      NuGet restore / install
            │
            ▼
 [GiftOfTheGivers ASP.NET Core Web App]
```

### D.1 Create Custom Helper Library & Publish to Azure Artifacts (10 Marks)

#### 1. Project Specifications
- **Project Name:** `GiftOfTheGivers.Helpers`
- **Project Type:** Class Library (`net9.0`)
- **Package Id:** `GiftOfTheGivers.Helpers`
- **Version:** `1.0.0`
- **Utility Methods Included:**
  1. `TaxCertificateHelper.FormatCertificateNumber(int donationId, DateTime? date)` — Formats SARS compliant Section 18A certificate numbers (`GOTG-18A-2026-00042`).
  2. `TaxCertificateHelper.CalculateTaxRelief(decimal amount, decimal rate = 0.28m)` — Computes SARS income tax rebate.
  3. `TaxCertificateHelper.GetPboRegistrationDetails()` — Returns official PBO number `930018993`.
  4. `TaxCertificateHelper.GenerateVerificationHash(...)` — Creates SHA256 anti-fraud digital verification token.
  5. `DonationMetricsHelper.FormatZar(decimal amount)` — Formats amounts to South African Rand (`R 1,500.00`).
  6. `DonationMetricsHelper.CalculateTotal(amounts)` — Aggregates donation totals.
  7. `DonationMetricsHelper.CalculateAverage(amounts)` — Calculates donation averages.
  8. `DonationMetricsHelper.GenerateDonorReference(name, id)` — Generates donor transaction codes.
  9. `DonationMetricsHelper.GetDonationTier(amount)` — Classifies donors into tiers (`Supporter`, `Partner`, `Guardian`, etc.).

#### 2. Creating the NuGet Package
The `.csproj` has `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>`.
To pack manually via command line:
```bash
dotnet pack GiftOfTheGivers.Helpers/GiftOfTheGivers.Helpers.csproj -c Release
```
Output package: `nupkg/GiftOfTheGivers.Helpers.1.0.0.nupkg`.

#### 3. Setting Up Azure DevOps Artifacts Feed
1. Go to [https://dev.azure.com](https://dev.azure.com) and sign in.
2. Create a new organization or project (e.g., `GiftOfTheGiversOrg` / `GiftOfTheGiversProject`).
3. In the left sidebar, click **Artifacts**.
4. Click **+ Create Feed**:
   - **Name:** `GiftOfTheGiversFeed`
   - **Visibility:** Members of your organization (or Project)
   - Click **Create**.
5. Click **Connect to Feed** → choose **dotnet**.
6. Generate a **Personal Access Token (PAT)**:
   - Click User Settings (top right avatar) → **Personal access tokens** → **+ New Token**.
   - Name: `ArtifactsPublishToken`
   - Scope: Under **Packaging**, select **Read & Write**.
   - Click **Create** and copy the token.

#### 4. Publishing the Package to Azure Artifacts
Open terminal/cmd in the solution folder and run:
```bash
# Add the Azure Artifacts source (one-time)
dotnet nuget add source "https://pkgs.dev.azure.com/YOUR-ORGANIZATION/_packaging/GiftOfTheGiversFeed/nuget/v3/index.json" --name "GiftOfTheGiversFeed" --username "AzureDevOps" --password "YOUR-PAT-TOKEN" --store-password-in-clear-text

# Push the package to your feed
dotnet nuget push nupkg/GiftOfTheGivers.Helpers.1.0.0.nupkg --source "GiftOfTheGiversFeed" --api-key az
```
Refresh your Azure Artifacts feed in the browser: `GiftOfTheGivers.Helpers` version `1.0.0` is now visible!

---

### D.2 Use the NuGet Package in the Web App (10 Marks)

#### 1. Installing into Web Application
In `GiftOfTheGivers.csproj`, the helper is integrated. To use from your Azure Artifacts feed:
```bash
dotnet add package GiftOfTheGivers.Helpers --version 1.0.0
```
Or via Visual Studio NuGet Package Manager:
- Right-click `GiftOfTheGivers` project → **Manage NuGet Packages...**
- Package source: Select **GiftOfTheGiversFeed**
- Search for `GiftOfTheGivers.Helpers` → click **Install**.

#### 2. Evidence of Usage in Web App Code
The helper methods are actively used across controllers and views:
- **`Controllers/DonationsController.cs`**:
  ```csharp
  // 1. Format official Section 18A certificate number
  donation.ReferenceNumber = TaxCertificateHelper.FormatCertificateNumber(donation.Id, donation.DonationDate);

  // 2. Compute aggregate metrics for donor history
  var total = DonationMetricsHelper.CalculateTotal(amounts);
  var average = DonationMetricsHelper.CalculateAverage(amounts);
  var taxRelief = TaxCertificateHelper.CalculateTaxRelief(total);
  ViewBag.TotalZar = DonationMetricsHelper.FormatZar(total);
  ViewBag.DonorTier = DonationMetricsHelper.GetDonationTier(total);
  ```
- **`Views/Donations/Certificate.cshtml`**:
  - Displays formatted amount via `DonationMetricsHelper.FormatZar(Model.Amount)`.
  - Displays SARS Section 18A tax deduction rebate.
  - Displays donor tier badge and tamper-evident verification token.
- **`Views/Donations/MyDonations.cshtml`**:
  - Displays total contribution, average donation, and SARS 18A tax relief cards.
- **`Areas/Staff/Controllers/StaffController.cs`**:
  - Computes total donations and average donation via `DonationMetricsHelper.CalculateTotal(amounts)`.

---

