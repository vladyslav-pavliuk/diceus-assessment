# DICEUS Claims Module: FNOL intake + reserve management

This is a vertical slice of the Claims module of an insurance Policy Administration System, built for the DICEUS senior full-stack
assessment. It covers:

- FNOL intake and the claim status state machine
- parties and risk objects
- event-sourced reserves with 3-tier approval authority
- an idempotent Hangfire GL-posting simulation and an SLA monitoring job
- document upload (Azure Blob Storage, with a local file-system fallback)
- an append-only audit log

Stack: .NET 9 (ASP.NET Core, MediatR 12, EF Core 9, FluentValidation, AutoMapper 14, Hangfire) and Angular 22 with Angular Material. It is
deployed to Azure Container Apps, a Static Web App, Azure SQL, Blob Storage and Key Vault by a GitHub Actions pipeline.

| | |
|---|---|
| **Frontend (SPA)** | https://witty-sea-048f76a03.2.azurestaticapps.net |
| **API / Swagger** | https://ca-claims-api.calmtree-6dea65c8.westeurope.azurecontainerapps.io/swagger |
| **Test users** | see [Test credentials](#test-credentials). No passwords: the demo signs users in with dev tokens (D-16) |
| **Architecture** | [ARCHITECTURE.md](ARCHITECTURE.md) |
| **AI workflow** | [AI-WORKFLOW.md](AI-WORKFLOW.md), per-phase logs in [docs/ai-log/](docs/ai-log/) |
| **Decisions** | [docs/DECISIONS.md](docs/DECISIONS.md): every gap or conflict between the brief and the FRS, with options and the choice made |
| **Definition of done** | [docs/REQUIREMENTS-MATRIX.md](docs/REQUIREMENTS-MATRIX.md): one row per requirement, with the code location and the tests |

The API scales to zero when idle (D-36), so the first request after a quiet period can take about a minute while the container starts and
the database resumes.

---

## 1. Prerequisites

| Tool | Version | Used for |
|---|---|---|
| .NET SDK | 9.0.x (`global.json`: 9.0.100, rolls forward to a later SDK) | backend build, tests, EF Core tooling |
| .NET runtime | 9.0 | running the API and the tests. With only a newer runtime installed, set `DOTNET_ROLL_FORWARD=Major` |
| Docker | Docker Desktop or Engine with Compose v2 | SQL Server 2022 and Azurite locally; the integration tests start their own containers with Testcontainers |
| Node.js | 24 (as in CI) with npm 11 | Angular build and tests |
| EF Core CLI | 9.0.20, pinned in `dotnet-tools.json` | `dotnet tool restore` installs it locally |
| Azure CLI, GitHub CLI | recent | only to reproduce the Azure deployment |

The SQL Server image is `linux/amd64`, so on Apple silicon Docker runs it under emulation (Rosetta).

## 2. Local development setup

Everything below runs from the repository root.

**Option A: API from source (recommended while developing)**

1. Start SQL Server 2022 and Azurite:
   ```bash
   docker compose up -d
   ```
2. Restore the local tools and create the database. This applies all migrations and the seed data; see §4.
   ```bash
   dotnet tool restore
   ```
   ```bash
   dotnet ef database update --project src/ClaimsModule.Persistence --startup-project src/ClaimsModule.API
   ```
3. Run the API on http://localhost:5080. Swagger is at `/swagger`, and the Hangfire dashboard at `/hangfire` (manager token required).
   ```bash
   dotnet run --project src/ClaimsModule.API
   ```
4. Run the SPA on http://localhost:4200. It calls `http://localhost:5080` (`src/environments/environment.development.ts`).
   ```bash
   cd web/claims-ui && npm ci && npm start
   ```

The Development settings (`src/ClaimsModule.API/appsettings.Development.json`) point at the compose SQL Server, turn dev tokens on, allow
CORS from `:4200`, and store documents on the local disk under `uploads/`.

**Option B: everything in containers**

```bash
docker compose --profile app up -d --build
```

This builds the production Dockerfile, runs the EF migrations bundle (`migrate` service), and then starts the API on http://localhost:5080.
Documents go to the container's disk. Run the SPA as in step 4.

**Tests**

```bash
dotnet test ClaimsModule.sln
```
```bash
cd web/claims-ui && npx ng lint && npx ng test --watch=false && npx ng build
```

The .NET suite has 692 tests (324 domain, 89 application, 279 integration). The integration tests need Docker: they start SQL Server 2022
and Azurite through Testcontainers and never touch the compose database. The Angular suite has 95 Vitest tests.

**End-to-end smoke test** (the brief §7.2 demo flow, over HTTP):

```bash
scripts/smoke-test.sh http://localhost:5080
```

## 3. Configuration (appsettings / environment variables)

Settings are standard ASP.NET Core configuration. In environment variables, `:` becomes `__`, for example
`Storage__AzureBlob__ServiceUri`.

| Key | Example (local) | Azure (set by `infra/api.bicep`) | Notes |
|---|---|---|---|
| `ConnectionStrings:ClaimsDb` | `Server=localhost,1433;Database=ClaimsModule;User Id=sa;Password=Local_dev_Passw0rd!;TrustServerCertificate=True` | Entra-only connection string (managed identity) | Required. Also holds Hangfire's `[HangFire]` schema |
| `Auth:Issuer` / `Auth:Audience` | `claims-module-api` / `claims-module` | same | JWT validation |
| `Auth:SigningKey` | `local-development-only-signing-key-change-me-in-azure` | Key Vault secret `auth-signing-key` (Container Apps secret reference) | HS256 key. Validated at startup |
| `Auth:TokenLifetime` | `08:00:00` | default | |
| `Auth:DevTokensEnabled` | `true` | `true` (demo, D-16) | `false` removes `/api/auth/dev-token` and `/api/auth/users` (they return 404) |
| `Cors:AllowedOrigins` | `["http://localhost:4200"]` | the Static Web App origin only | |
| `Storage:Provider` | `LocalFileSystem` | `AzureBlob` | `AzureBlob` \| `LocalFileSystem` (BR-D-03) |
| `Storage:AzureBlob:ConnectionString` | `UseDevelopmentStorage=true` (Azurite) | not set | account-key SAS; local only |
| `Storage:AzureBlob:ServiceUri` | not set | `https://stclaims<suffix>.blob.core.windows.net` | managed identity + user-delegation SAS |
| `Storage:AzureBlob:ContainerName` | `claim-documents` | `claim-documents` | |
| `Storage:LocalFileSystem:RootPath` | `uploads` | not used | files go under `{root}/{orgId}/{claimId}/` |
| `Storage:LocalFileSystem:PublicBaseUrl` | `http://localhost:5080` | not used | base of the signed local download links |
| `Jobs:RunServer` | `true` | `true` | runs the in-process Hangfire server |
| `Jobs:GlPosting:SimulateFailure` | `false` | `false` | `true` makes the simulated ledger fail, to demonstrate retries → GL_POSTING_FAILED → retry |
| `Serilog:*` | see `appsettings.json` | JSON console output to Log Analytics | every log line carries the correlation id |

Outside Development, the API refuses to start with an unusable storage configuration (`BR_D_03_Outside_development_…` test). The SPA's API
URL is a build-time constant: `ng build --define "API_BASE_URL='https://…'"` (D-44).

## 4. Database migrations and seed data

- The schema comes only from EF Core migrations in `src/ClaimsModule.Persistence/Migrations`. The app creates no schema at runtime.
- Seed data is in the migrations (`HasData`, from `src/ClaimsModule.Persistence/Seed/SeedData.cs`). There is no startup seeding. It
  contains:
  - 1 organisation;
  - 6 users (two per role);
  - the 5 FRS §5.5 policies plus 3 long-dated extras (D-34);
  - the 10 FRS §5.6 cause-of-loss codes;
  - the 12 status transitions of FRS §4.2 (D-09).
- The migrations also add the audit-log `INSTEAD OF UPDATE, DELETE` trigger (D-14).
- Apply the migrations with `dotnet ef database update …` (§2). The Docker `migrator` target builds a self-contained EF migrations bundle,
  used by compose and by the deploy pipeline. It reads the connection from `ConnectionStrings__ClaimsDb`.
- Hangfire creates its own `[HangFire]` schema on first start.
- `CONV_11_Migrations_build_the_database_from_zero` proves a fresh database can be built from zero, and
  `CONV_11_Model_has_no_changes_missing_from_the_migrations` proves no model change is missing from the migrations. Checked again by hand in
  Phase 8: 8 policies, 10 codes, 12 transitions, 6 users.
- To add a migration:
  ```bash
  dotnet ef migrations add <Name> --project src/ClaimsModule.Persistence --startup-project src/ClaimsModule.API
  ```

## 5. Azure deployment

| Resource | Purpose |
|---|---|
| Azure Container Apps (Consumption, 0–1 replicas) | ASP.NET Core API + in-process Hangfire server; image from `ghcr.io` |
| Azure Static Web App (Free) | Angular SPA |
| Azure SQL Database (serverless, free offer, auto-pause) | application tables + Hangfire schema; Entra-only authentication |
| Storage account, container `claim-documents` | documents; downloads use 1-hour user-delegation SAS URLs, so the bytes never pass through the API |
| Key Vault | the JWT signing key |
| User-assigned managed identity | the API's only credential (SQL, Blob Storage, Key Vault). No connection secrets |
| Log Analytics | container logs (JSON, with the correlation id) |

The brief names App Service; the API runs on Container Apps instead, which brief §2.3 allows. The reason is cost: scaling to zero stops
Hangfire's SQL polling, so the serverless database can pause (D-36).

**Reproduce it:**

- Templates: `infra/main.bicep` (platform) and `infra/api.bicep` (the Container App).
- One-time setup (resource group, app registration + OIDC federated credential, repository variables): `infra/bootstrap.sh`.
- Pipeline: `.github/workflows/deploy.yml`, run manually. In order it does:
  - CI;
  - image → ghcr.io;
  - Bicep;
  - EF migrations bundle against Azure SQL;
  - the least-privilege database user for the API;
  - the Container App revision;
  - the SPA build and upload;
  - the smoke test.
- GitHub stores no secrets: Azure sign-in uses OIDC.
- `.github/workflows/ci.yml` builds and tests on every push.

The step-by-step runbook, with the manual steps, the live-review checklist (`minReplicas 1`), troubleshooting and teardown, is in
**[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)**. After the first-time setup, a deployment is:

```bash
gh workflow run deploy.yml --ref main
```

## 6. Application walkthrough

**Claims list** (`/claims`):
- server-side paging;
- filters for status (multi-select), loss-date range, assigned handler and cause of loss, all kept in the URL;
- colour-coded status badges;
- reserve total per row;
- "Log New Claim".

**FNOL intake** (`/claims/new`), a 3-step stepper:
1. Policy typeahead with an in-force badge, an "Unknown policy" toggle, loss date and time (no future dates), cause of loss, description
   (at least 20 characters) and location.
2. Parties (at least one Claimant) and risk objects.
3. An optional initial reserve with a live authority indicator, then a review with a warnings dialog.

On success the snackbar shows the generated `CLM-YYYY-0000000` number and opens the claim.

**Claim detail** (`/claims/:id`): a header with the claim number (copy), status chip, policy, loss date, cause and handler. The status menu
offers only the valid next statuses; confirmation dialogs guard each change, and a closure pre-flight checks CC-01..04. There are five tabs:
- **Overview:** loss details, notes, severity, and validation issues with Acknowledge.
- **Parties:** add; remove, disabled for the last Claimant.
- **Reserves:** summary cards, the full transaction history, an Add Reserve panel with the authority indicator, Approve/Reject for
  supervisors and managers, Retract for the submitter, and the GL posting badge with retry.
- **Documents:** upload with progress; download through a 1-hour signed URL.
- **Audit log:** reverse-chronological and paged.

**Rules worth demonstrating:**

| Rule | What happens |
|---|---|
| Reserve authority is based on the single transaction's absolute amount | ≤ $10,000 is auto-approved; > $10,000 needs a supervisor or manager; > $100,000 needs a manager |
| BR-R-03 | Self-approval gives 422 "Self-approval is not permitted." |
| BR-R-05 | Approved reserves may not exceed $10M per claim without a manager override |
| GL posting | Every approval triggers a Hangfire job that writes GL_POSTING_SIMULATED exactly once |
| SLA job | Every 15 minutes it flags Draft/Open claims with no update for 48 hours in the audit log, without changing their status |

The toolbar's user switcher changes the signed-in user and role.

**API surface:**
- `POST/GET /api/claims`;
- `GET /api/claims/{id}`;
- `PUT /api/claims/{id}/status`;
- `GET /api/claims/{id}/audit`;
- `POST/GET /api/claims/{id}/documents`;
- `POST/PUT/GET /api/claims/{id}/reserves[...]` with `/approve`, `/reject`, `/retract` and `/retry-posting`;
- `GET /api/policies/search`;
- `GET /api/policies/{id}/coverage`;
- `GET /api/reference/cause-of-loss-codes`;
- `GET /api/reference/claim-statuses`;
- the supporting endpoints listed in D-08.

Every write accepts an `Idempotency-Key` header. Errors are ProblemDetails; validation failures are 422 with the FRS §10.4 body.

### Test credentials

There are no passwords: the demo issues real signed JWTs for seeded users (D-16).
- **SPA:** it signs in as `handler.alex`; use the toolbar switcher to change user.
- **Swagger:** call `POST /api/auth/dev-token` with `{"username": "supervisor.casey"}`, then **Authorize** with the returned token.

| Role | Users | Can |
|---|---|---|
| handler | `handler.alex`, `handler.blake` | create claims, manage parties and documents, submit reserves (auto-approved up to $10,000), most status transitions |
| supervisor | `supervisor.casey`, `supervisor.drew` | plus approve/reject reserves up to $100,000, reassign the handler, reopen closed claims |
| manager | `manager.emery`, `manager.finley` | plus approve above $100,000, set the $10M reserve-limit override, Hangfire dashboard (`/hangfire?access_token=<token>`) |

There are two users per role, so a reviewer can show that self-approval is refused and cross-approval works at every tier.
