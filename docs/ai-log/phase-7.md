# AI log: Phase 7 (Azure deployment and CI/CD)

- **Date:** 2026-09-28 (one session).
- **Tool:** Claude Code (desktop app), model Claude Opus 5.5. Web lookups (Microsoft Learn, GitHub releases API) to verify Azure and GitHub facts;
  Bicep CLI 0.47.16, actionlint and shellcheck installed locally for linting. **No Azure resource was created**: the local `az` session was used only
  to install the Bicep CLI.
- **Context loaded:** `CLAUDE.md`, `docs/PROMPTS.md` (Phase 7), brief §3.8, §4.2–4.3, §7.2; `docs/DECISIONS.md` (D-14–D-16, D-28, D-35–D-43);
  `docs/ARCHITECTURE-PLAN.md` §7; `docs/REQUIREMENTS-MATRIX.md` §13; the Dockerfile, docker-compose.yml, `Program.cs`, the API/Infrastructure/Persistence
  DI, storage options and `AzureBlobStorageService`, Hangfire registration, the `InitialCreate` audit trigger and `claims_app` role, the controllers and
  request contracts, the integration-test `ClaimsApi` helper, the Angular environments and `angular.json`.

## 1. Prompt given (verbatim)

```
Phase 7: Azure + GitHub Actions (brief §3.8).
- infra/: Bicep (preferred) or a documented az CLI script for App Service (Linux), Static Web App, Azure SQL
  (free/serverless), Storage account + claim-documents container, Key Vault, and a managed identity with
  the correct RBAC (Storage Blob Data Contributor + Storage Blob Delegator; Key Vault Secrets User).
- .github/workflows/deploy.yml (workflow_dispatch acceptable): build + test the backend; build an EF migrations
  bundle and run it against Azure SQL; build Angular with the production API URL; deploy the API and the SWA.
  Use OIDC federated credentials rather than stored publish profiles if feasible.
- Swagger enabled in the deployed environment; CORS locked to the SWA origin; Hangfire storage on Azure SQL.
- A smoke-test script covering the brief §7.2 demo flow: create a claim → add a reserve over 10k as a handler →
  approve as a supervisor → the GL job posts → upload a document → the audit log shows everything.
Tell me exactly which steps I must run manually (subscription, secrets, federated credential). Stop and summarise.
```

**Conflict with an accepted decision.** The prompt says App Service (Linux); D-36 (changed by Vlad on 2026-09-27) says Container Apps, and CLAUDE.md
says DECISIONS.md wins. Claude built Container Apps and says so in D-44's context rather than asking again.

## 2. Requirement IDs touched
DEL-05, DEL-06, DEL-10, API-DOCS, BR-D-02 (user-delegation SAS path), BR-A-01 / D-14 (the `claims_app` DENY now applies: the API is no longer dbo),
SEC (CORS, dev tokens D-16). Decisions applied: D-14, D-16, D-28, D-35, D-36, D-38, D-39 item 15, D-41 item 14, D-42. New **D-44** (Q1–Q3 open, 17 items
PROPOSED); D-36 amended (user-assigned identity, no SQL secret; 15-minute auto-pause verified).

## 3. What was generated

| Area | Files |
|---|---|
| Bicep | `infra/main.bicep` (identity, Log Analytics with a daily cap, Container Apps environment, Key Vault RBAC, Storage with shared keys off + soft delete + container, SQL server Entra-only + free-offer serverless database + firewall, Static Web App, four role assignments, outputs), `infra/api.bicep` (Container App: Key Vault secret reference, env config, probes, scale 0–1) |
| SQL | `infra/sql/grant-api-identity.sql`: idempotent database user for the managed identity (`WITH SID, TYPE = E`), reader/writer + `claims_app` + `CREATE TABLE` + owner of `[HangFire]`; optional read-only user for Vlad |
| Bootstrap | `infra/bootstrap.sh`: providers, resource group, app registration + SP, federated credential (main branch), Contributor + RBAC Administrator, repository variables |
| Workflows | `.github/workflows/ci.yml` (push/PR/`workflow_call`: .NET build + tests, Angular lint/tests/build), `.github/workflows/deploy.yml` (`workflow_dispatch`: ci → image ∥ infra → api ∥ web → smoke) |
| Frontend | `environment.ts` reads the build-time constant `API_BASE_URL` (default in `angular.json`), `public/staticwebapp.config.json` (deep-link fallback, cache and security headers) |
| Smoke test | `scripts/smoke-test.sh` (brief §7.2 flow, cold-start wait, Swagger check, SAS download compared byte for byte, audit assertions) |
| Docs | D-44 (+ D-36 amendments), `docs/DEPLOYMENT.md` (runbook: manual steps, pipeline, review warm-up, troubleshooting, teardown), ARCHITECTURE-PLAN §7 rewritten to what was built, REQUIREMENTS-MATRIX DEL-05/06/10, API-DOCS, BR-D-02, REVIEW-PREP Phase 7 (15 Q&A), this log |

No backend C# changed: the API already read everything from configuration (storage service URI + DefaultAzureCredential, connection string, CORS
origins, signing key), and forwarded headers are enabled by an environment variable.

## 4. Verification that ran
- **Bicep:** `az bicep build` on both templates, 0 errors, 0 linter warnings (so `useFreeLimit` / `freeLimitExhaustionBehavior` exist on
  `Microsoft.Sql/servers/databases@2023-08-01`).
- **Workflows:** actionlint clean (after one fix, §5). **Scripts:** shellcheck clean, `bash -n` clean.
- **Migrations bundle:** the workflow's exact `dotnet ef migrations bundle … --self-contained --target-runtime linux-x64` in `mcr.microsoft.com/dotnet/sdk:9.0`
  (linux/amd64) produced a 141 MB `efbundle` that runs.
- **go-sqlcmd v1.10.0:** the workflow's exact flags (`--server`, `--database-name`, `--exit-on-error`, `--input-file`, repeated `-v`) ran the grant script
  against a probe database; `--authentication-method` is listed in its help.
- **Least-privilege database user**, against the compose SQL Server 2022: fresh database migrated as `sa`, a SQL-auth user standing in for the managed
  identity, the grant script run twice (idempotent), the API container run as that user: Hangfire created its schema, the **smoke test passed**, and
  `UPDATE ClaimAuditLog` (Msg 229), `DISABLE TRIGGER` (1088), `DROP TRIGGER` (3701), `CREATE TABLE dbo.Evil` (2760), `ALTER TABLE dbo.Claims` (1088) were
  refused while `CREATE TABLE HangFire.x` was allowed. Test database, login and container removed afterwards.
- **Smoke test** against the running compose API (`http://localhost:5080`): passed, all seven steps (handler's approve → 403; GL Posted; signed URL bytes identical).
- **CORS check** from the workflow, run by hand against the local API: the allowed origin is echoed, `https://evil.example` gets no header.
- **Angular:** `ng test` 95/95, `ng lint` clean, `ng build` (default URL `localhost:5080` in `main-*.js`), `ng build --define "API_BASE_URL='https://ca-test.example.io'"`
  (the override in `main-*.js`); `staticwebapp.config.json` is copied into `dist/claims-ui/browser`.
- **Backend:** `dotnet build` 0 warnings; `dotnet test` **692/692** (324 domain, 89 application, 279 integration), host SDK 10 with `DOTNET_ROLL_FORWARD=Major`.
- **Facts checked on the web** rather than recalled: serverless auto-pause minimum is 15 minutes; SQL free offer = 100,000 vCore-s + 32 GB per database, up to 10
  per subscription, "continue using database for additional charges" option; a service-principal Entra admin's `sid` is its application (client) id;
  go-sqlcmd latest is v1.10.0 with a `sqlcmd-linux-amd64.tar.bz2` asset; current majors of every action used (checkout v7, setup-dotnet v6, setup-node v7,
  azure/login v3, upload-artifact v7, docker login v4 / buildx v4 / build-push v7, static-web-apps-deploy v1).
- **Not verified (no deployment made):** Entra token acquisition by the bundle and go-sqlcmd on a GitHub runner, `CREATE USER … TYPE = E` on Azure SQL, the
  Key Vault secret reference, the user-delegation SAS, RBAC propagation timing, `bootstrap.sh` itself, and the Static Web App upload.

## 5. What Claude got wrong, and what changed

No correction came from Vlad in this session. These are Claude's own mistakes, found by linting, a test run or a re-read, and fixed before the summary:

1. **GitHub environment on a private repository.** The first `deploy.yml` put the Azure jobs in `environment: production` and planned a federated
   credential with an `environment:production` subject. The repository is private, and environments are not available for private repositories on
   GitHub Free. Removed; the credential trusts `refs/heads/main` instead, and D-44 Q3 asks Vlad about it.
2. **Wrong role count** in D-44 item 17 ("the five roles the templates assign"); the templates assign four. Fixed.
3. **`gh run rerun --failed` without a run id** in the first DEPLOYMENT.md; the command needs one. Fixed with a lookup of the latest run.
4. **actionlint / shellcheck SC2015** in the smoke job: `A && B || C` used as if-then-else. Rewritten as an `if`.
5. **Test-harness slips (not shipped code):** a zsh one-liner relied on word splitting of a command stored in a variable; a SQL login password contained
   the login name and failed the password policy; `timeout` does not exist on macOS; the first restricted-user API run used `ASPNETCORE_ENVIRONMENT=Production`
   with local file storage, which the API correctly refuses to start with (`BR_D_03_Outside_development_…`); re-run as Development, since storage was not
   what that test measured.

Where Claude departed from written text, it did not decide silently: App Service → Container Apps follows D-36; D-44 **Q1** (passwordless SQL), **Q2**
(API not dbo) and **Q3** (no GitHub environment) are open for Vlad, implemented as recommended.

## 6. Open items for Vlad
- Decide D-44 Q1–Q3; review items 1–17.
- Run the manual steps in `docs/DEPLOYMENT.md` (push `main`, `az login`, `gh auth login`, `infra/bootstrap.sh`, first workflow run, make the ghcr package
  public, re-run). Then record the real run here: what failed on Azure, if anything.
- `.claude/launch.json` is still uncommitted (not decided in Phase 6).

## 7. First run on Azure (2026-09-28)
Vlad ran `infra/bootstrap.sh` (all repository variables set) and then `gh workflow run deploy.yml --ref main`.

1. **HTTP 404, "workflow deploy.yml not found on the default branch".** The workflows were only on `phase-7-azure`; the runbook said "push `main`"
   without saying that Phase 7 had to be merged first. Vlad merged `phase-7-azure` into `main` and pushed (`eab37f8`). DEPLOYMENT.md step 2 now says why.
2. **Run 36429843805 failed in two jobs:**
   - `image`: *Check the image is publicly pullable*, as designed: the new ghcr package is private (manual step 5).
   - `infra`: `azure/login` → `AADSTS700213: No matching federated identity record found for presented assertion subject
     'repo:vladyslav-pavliuk@64861208/diceus-assessment@1390636621:ref:refs/heads/main'`. **Claude's mistake:** `bootstrap.sh` hard-coded the name-only
     subject `repo:<owner>/<repo>:ref:refs/heads/main`, but this repository issues GitHub's immutable subjects (owner and repository ids in the claim).
     Claude had not checked the repository's OIDC settings. Fix: `bootstrap.sh` reads `sub_claim_prefix` from `GET repos/{repo}/actions/oidc/customization/sub`,
     refuses a custom subject template, and updates an existing credential whose subject differs. Verified that the computed subject equals the one in the
     error. D-44 Q3 and DEPLOYMENT.md (manual table, troubleshooting) updated.
