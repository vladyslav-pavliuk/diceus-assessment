# Azure deployment runbook

How the demo environment is built and how to reproduce it (brief §3.8, §4.2 item 5). Decisions: D-36 (hosting), D-44 (this phase).

## Current deployment

| | URL |
|---|---|
| SPA | https://witty-sea-048f76a03.2.azurestaticapps.net |
| API | https://ca-claims-api.calmtree-6dea65c8.westeurope.azurecontainerapps.io |
| Swagger | https://ca-claims-api.calmtree-6dea65c8.westeurope.azurecontainerapps.io/swagger |
| Hangfire (manager token) | https://ca-claims-api.calmtree-6dea65c8.westeurope.azurecontainerapps.io/hangfire |

Resource group `rg-claims-demo` (westeurope). First green run: 36446032128 (2026-09-28).

## What gets deployed

| Resource | Name | Tier | Role |
|---|---|---|---|
| Container Apps environment + app | `cae-claims`, `ca-claims-api` | Consumption, 0.5 vCPU / 1 GiB, 0–1 replicas | ASP.NET Core API + in-process Hangfire server |
| User-assigned managed identity | `id-claims-api` | — | the API's only credential (SQL, Blob Storage, Key Vault) |
| Azure SQL server + database | `sql-claims-<suffix>` / `claims` | serverless Gen5 0.5–2 vCores, **free offer**, auto-pause 15 min | application tables + `[HangFire]` schema; Entra-only auth |
| Storage account + container | `stclaims<suffix>` / `claim-documents` | Standard LRS, shared keys disabled | documents; downloads are 1-hour user-delegation SAS |
| Key Vault | `kv-claims-<suffix>` | Standard, RBAC | `auth-signing-key` (JWT HS256 key) |
| Static Web App | `swa-claims-<suffix>` | Free (westeurope) | Angular SPA |
| Log Analytics | `log-claims-<suffix>` | PerGB2018, 0.5 GB/day cap | container logs (JSON, with correlation id) |

`<suffix>` = `uniqueString(resourceGroup().id)`. Templates: `infra/main.bicep` (platform) and `infra/api.bicep` (the Container App).

```
Browser ──► Static Web App (SPA) ──(Bearer JWT, CORS: SWA origin only)──► Container App (API + Hangfire)
                                                                             │  managed identity id-claims-api
                                                                             ├─► Azure SQL (Entra token; plain DB user)
                                                                             ├─► Blob Storage (container-scoped Data Contributor + Delegator)
                                                                             └─► Key Vault (Secrets User, via the Container Apps secret reference)
Browser ──► SAS URL ──► Blob Storage (bytes never pass through the API, BR-D-02)
```

## Pipeline (`.github/workflows/deploy.yml`, manual trigger)

```
ci ──┬── image (ghcr.io) ─────────────┐
     └── infra ──┬─────────────── api ─┬── smoke
                 └── web (SWA) ────────┘
```

| Job | What it does |
|---|---|
| `ci` | `ci.yml`: .NET build (warnings are errors) + 692 tests incl. Testcontainers; Angular lint, 95 unit tests, production build |
| `image` | builds the Dockerfile's `runtime` target, pushes `ghcr.io/<owner>/claims-api:<sha>`, checks it is anonymously pullable |
| `infra` | `main.bicep` → creates the signing key once → EF Core migrations bundle against Azure SQL → `infra/sql/grant-api-identity.sql` (runner IP allowed only for this job) |
| `api` | `api.bicep` with the new image and `min_replicas`; waits for `/health/ready` |
| `web` | `ng build --define "API_BASE_URL='<api url>'"`, uploads to the SWA with a token read at run time |
| `smoke` | SPA deep links, CORS (SWA origin admitted, a foreign origin refused), `scripts/smoke-test.sh` (brief §7.2 flow) |

Sign-in to Azure is OIDC (federated credential). GitHub stores **no secrets** for this pipeline, only ids as repository variables.

## Manual steps (one time)

Everything below needs a human with rights no pipeline should have. After step 5 every deployment is `gh workflow run`.

1. **Subscription.** An Azure subscription where you are **Owner** (any type except "Azure for Students Starter", which the SQL free offer
   excludes). The SQL free offer allows 10 free databases per subscription, all in one region; if you already use it in another region, pass that region.
2. **Push the code.** The GitHub repository's `main` must contain the workflows (GitHub runs a manual workflow only if it is on the default branch, and the federated credential trusts `refs/heads/main` only; D-44 Q3):
   ```bash
   git push -u origin main
   ```
3. **Sign in locally.**
   ```bash
   az login
   ```
   ```bash
   az account set --subscription "<subscription name or id>"
   ```
   ```bash
   gh auth login
   ```
4. **Bootstrap** (resource providers, resource group, app registration + service principal + federated credential, its roles on the resource
   group, repository variables). Idempotent; it prints what it will use and asks before doing anything:
   ```bash
   infra/bootstrap.sh --location westeurope --resource-group rg-claims-demo
   ```
   It sets these repository variables: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `AZURE_SP_OBJECT_ID`,
   and (unless `--no-sql-reader`) `SQL_READER_NAME` / `SQL_READER_OBJECT_ID`, which give **you** a read-only database user for the portal query editor.
5. **First deployment, then make the image public.**
   ```bash
   gh workflow run deploy.yml --ref main
   ```
   The first run fails at *Check the image is publicly pullable*, by design: a new GHCR package is private, and GitHub has no API to change that.
   Open github.com → your profile → **Packages** → `claims-api` → **Package settings** → **Change visibility** → **Public**. Then re-run the failed jobs:
   ```bash
   gh run rerun "$(gh run list --workflow deploy.yml --limit 1 --json databaseId --jq '.[0].databaseId')" --failed
   ```
   The job summary of the `smoke` job lists the SPA, API, Swagger and Hangfire URLs.

### If you prefer to do step 4 by hand

| What | Command / value |
|---|---|
| Providers | `az provider register --namespace <ns> --wait` for Microsoft.App, OperationalInsights, ManagedIdentity, KeyVault, Storage, Sql, Web |
| Resource group | `az group create -n rg-claims-demo -l westeurope` |
| App registration + SP | `az ad app create --display-name github-claims-deploy`, then `az ad sp create --id <appId>` |
| Federated credential | issuer `https://token.actions.githubusercontent.com`, subject `<sub_claim_prefix>:ref:refs/heads/main`, audience `api://AzureADTokenExchange`. Take the prefix from `gh api repos/<owner>/<repo>/actions/oidc/customization/sub`: newer repositories use immutable subjects such as `repo:owner@123/name@456` |
| Roles (scope: the resource group) | `Contributor` and `Role Based Access Control Administrator` for the SP |
| Repository variables | as listed in step 4 (`gh variable set NAME --body VALUE`) |

## Every later deployment

```bash
gh workflow run deploy.yml --ref main
```

Inputs: `min_replicas` (`0` default, scale to zero; `1` always warm) and `run_smoke_test` (default on; creates one demo claim per run).

## Before and after the live review

Cold start after idle = container start + database resume (up to about a minute, D-36). Before the session:

```bash
az containerapp update --name ca-claims-api --resource-group rg-claims-demo --min-replicas 1
```

then open the SPA once (wakes the database) or run the smoke test. Afterwards, back to scale to zero:

```bash
az containerapp update --name ca-claims-api --resource-group rg-claims-demo --min-replicas 0
```

(A redeploy with `min_replicas=1` does the same; a plain redeploy resets it to 0.)

## Test users (D-16)

The SPA signs in as `handler.alex` and the toolbar switches user. Swagger: `POST /api/auth/dev-token` with `{"username": "…"}`, then **Authorize**.

| Role | Users |
|---|---|
| handler | `handler.alex`, `handler.blake` |
| supervisor | `supervisor.casey`, `supervisor.drew` |
| manager | `manager.emery`, `manager.finley` (Hangfire dashboard: `/hangfire?access_token=<token>`) |

## Smoke test by hand

```bash
scripts/smoke-test.sh https://ca-claims-api.<environment domain>
```

It also runs locally against docker compose (`scripts/smoke-test.sh http://localhost:5080`).

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `azure/login` fails with `AADSTS700213` / `AADSTS70021`, no matching federated identity | the credential's subject differs from the token's `subject claim` (printed in the log). Either the run was not on `main`, or the credential uses the name-only form while the repository issues immutable subjects (`repo:owner@<id>/repo@<id>:…`). Re-run `infra/bootstrap.sh`: it reads the prefix from GitHub and corrects the credential |
| `AuthorizationFailed` on a role assignment in `main.bicep` | the SP lacks *Role Based Access Control Administrator* on the resource group (bootstrap step) |
| Key Vault `Forbidden` on the first run | role assignment still propagating; the step retries for 3 minutes |
| API revision stuck *Activating*, "unable to fetch secret" | the identity's Key Vault role is still propagating on the very first run; the `api` job retries |
| API revision fails to pull the image | the GHCR package is private (step 5) |
| Migrations: `Login failed for user '<token-identified principal>'` | the SP is not the SQL Entra admin: `main.bicep` did not run, or `AZURE_CLIENT_ID` is not the app registration's client id |
| API `/health/ready` 503, logs show `Login failed … id-claims-api` | the grant script did not run; re-run the `infra` job |
| First request after idle takes ~1 minute | expected with `minReplicas = 0` (D-36); see *Before the live review* |

## Tear down

```bash
az group delete --name rg-claims-demo --yes
```
```bash
az keyvault purge --name kv-claims-<suffix>
```
```bash
az ad app delete --id <AZURE_CLIENT_ID>
```

The purge frees the Key Vault name (soft delete keeps it for 7 days). Delete the `claims-api` package on GitHub if it is no longer needed.
