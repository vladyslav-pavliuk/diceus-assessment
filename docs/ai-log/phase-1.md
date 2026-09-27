# AI log: Phase 1 (solution skeleton & cross-cutting concerns)

- **Date:** 2026-09-27
- **Tool:** Claude Code (desktop app), model Claude Opus 5.5.
- **Context loaded:** `CLAUDE.md`, `docs/DECISIONS.md` (re-read in full, as the prompt asked), `docs/PROMPTS.md`, `docs/ARCHITECTURE-PLAN.md`,
  `docs/REQUIREMENTS-MATRIX.md`, FRS §3, §10, §10.4, §11.4, §14, §15, brief §2.3, §2.4, §3.2, §3.7.4, §3.8, §4.1, §4.2, §6.1, §6.2.

## 1. Prompt given (verbatim: PHASE 1 from `docs/PROMPTS.md`)

```
Phase 1. DECISIONS.md is accepted with my edits (re-read it). Build the solution skeleton only:
- The 5 src projects + 3 test projects with correct references (enforce the dependency rule; add an
  architecture test with NetArchTest or similar).
- Directory.Build.props (nullable, warnings-as-errors for new code, C# 13), central package management with pinned
  versions (MediatR 12.x, AutoMapper 14.x — confirm the licences).
- API: ProblemDetails exception middleware (422 shape exactly as FRS §10.4), correlation-id middleware,
  structured logging, Swagger with the JWT security scheme, health check, CORS for the SPA.
- Application: ValidationBehavior, LoggingBehavior, ICurrentUser, IUnitOfWork, IAuditLogService,
  IStorageService, IBackgroundJobScheduler abstractions. Use TimeProvider.
- Mock auth: seeded users (per D-16), POST /api/auth/dev-token issuing a signed JWT with sub, name, role, and
  org claims; signing key from config (Key Vault in Azure); role policies "Handler", "Supervisor",
  "Manager".
- docker-compose.yml: SQL Server 2022 + Azurite (+ the API, optionally).
List the requirement IDs this phase touches, write the tests, and run build + test. Stop and summarise.
```

## 2. Requirement IDs touched
API-28, API-ERR, API-AUTH, API-CORR (partial: audit stamping is Phase 2), API-DOCS (partial: Azure URL is Phase 7), API-LOG, OPS-01, OPS-02 (new rows),
SEC-01, SEC-02 (partial: policy mechanism only), SEC-05, CONV-09, CONV-11, CONV-13, CONV-14, CONV-15 (partial: UoW is Phase 2), CONV-16 (new row), DEL-07.
Decisions applied: D-08, D-09 (role hierarchy), D-12, D-14 (audit table has no soft delete; not yet built), D-16, D-24 (CORS header), D-30, D-31, D-36, D-37.

## 3. What was generated
| Area | Files |
|---|---|
| Build | `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `tests/Directory.Build.props`, `dotnet-tools.json`, `ClaimsModule.sln`, 8 `.csproj` |
| Domain | `Entity`, `BusinessRuleViolationException`, `Organisation`, `User`, `UserRole` (+ `IsAtLeast`, FRS §3 codes) |
| Application | `ICommand`/`IQuery` markers; `ValidationException`, `NotFoundException`, `ForbiddenAccessException`; `LoggingBehavior`, `ValidationBehavior`; abstractions `ICurrentUser`, `ICorrelationContext`, `IUnitOfWork`, `IAuditLogService`, `IStorageService`, `IBackgroundJobScheduler`, `ITokenService`, `IUserRepository`; `GetUserByUsernameQuery`, `ListDemoUsersQuery`, `UserDto` + AutoMapper profile |
| Infrastructure | `AuthOptions` (validated on start), `JwtTokenService` (TimeProvider-based), `CorrelationContext`, `TimeProvider.System` registration |
| Persistence | `ClaimsDbContext` (pre-conventions: decimal 19,4, datetimeoffset(7), enums → NVARCHAR(50)), shadow audit/soft-delete columns + filter convention, Organisation/User configurations with `HasData` seed (1 org, 6 users per D-16), `UserRepository`, DB health check, `InitialCreate` migration |
| API | `Program.cs`, `ExceptionHandlingMiddleware`, `CorrelationIdMiddleware`, `ApiProblems` (one error shape), `HttpCurrentUser`, `AuthorizationPolicies`, JwtBearer + Swagger + CORS + ProblemDetails setup, `AuthController`, `appsettings*.json` |
| Docker | `Dockerfile` (runtime + migrator targets), `.dockerignore`, `docker-compose.yml` (SQL Server 2022, Azurite; `app` profile: migrate + API) |
| Tests | Domain 17, Application 12, Integration/architecture 55 (Testcontainers SQL Server + WebApplicationFactory; NetArchTest; Mono.Cecil IL scan) |
| Docs | D-37 verification + CVE amendment, D-38 (PROPOSED), matrix statuses, ARCHITECTURE-PLAN corrections, REVIEW-PREP Phase 1 section, this log |

**Verification that ran:**
- `dotnet build` + `dotnet test` of the whole solution on the **.NET 9 SDK 9.0.318** in `mcr.microsoft.com/dotnet/sdk:9.0`: 0 warnings (warnings are errors), 84/84 tests pass.
- The same suite on the host (SDK 10.0.300, runtime rolled forward).
- `docker compose --profile app up --build`: the migrations bundle applied `InitialCreate` and exited 0; the API ran as uid 1654 (non-root).
- `curl` smoke test: live 200, ready 200, dev-token 200, exact 422 body, anonymous unknown route 401, Swagger 200, correlation id on every log line of the request.

## 4. What Vlad decided during the phase
Claude stopped once to ask (CLAUDE.md: "If a conflict is not yet listed there, stop and ask").
- **AutoMapper 14.0.0 CVE-2026-32933 (high)** made the build fail under warnings-as-errors. No 14.x patch exists; the fixes are in the commercial majors.
  Options offered: suppress only this advisory, with guard tests (recommended) / upgrade to 16.x with a licence key / downgrade NU1903 to a warning solution-wide.
  **Vlad chose the recommended option.** Recorded as the D-37 amendment. Claude then wrote the two guard tests the suppression relies on.

## 5. Where Claude refined or corrected the plan or the context files
- **CLAUDE.md said AutoMapper 14 is "Apache-licensed".** The nuget.org catalog says **MIT**. In Phase 0, Claude had copied the claim into D-37 without checking.
  Corrected in CLAUDE.md and D-37.
- **D-08 said the dev-token endpoint "does not go through MediatR".** Refined: the user lookup is a MediatR query (CLAUDE.md rule 1); only signing is outside (D-38 item 7).
- **ARCHITECTURE-PLAN had `UX (OrganisationId, Username)`.** Changed to a system-wide unique username, because sign-in happens before the tenant is known (D-38 item 6).
- **Unknown routes:** Claude's first test expected 404. The fallback authorization policy returns 401 to anonymous callers for unmatched routes as well.
  Claude kept the behaviour (it does not leak which routes exist), split the test into 401 (anonymous) and 404 (signed in), and documented it (D-38 item 3).

## 6. What Claude got wrong (honest record, all caught before the phase ended)
1. **Scaffolding:** used `dotnet new … -f net9.0`. The only local SDK is 10, whose templates cannot target net9.0. Fixed by writing the project files by hand.
2. **Seeding:** used `Dictionary<string, object>` rows in `HasData`. EF treated the dictionary as an object without an `Id` and failed at design time. Fixed with
   anonymous objects (the supported way to seed private setters and shadow properties).
3. **Inconsistent limit:** the username validator allowed 100 characters while the column is NVARCHAR(255). Claude's own boundary test caught it; the validator now says 255.
4. **MediatR 12.5 API:** Claude assumed `RequestHandlerDelegate<T>` was parameterless. In 12.5 it takes a `CancellationToken`; the behaviours now pass it through.
5. **`[Produces("application/json")]` on `AuthController`** silently changed the content type of its 401/422 problem responses from `application/problem+json`.
   Caught by the integration tests; the attribute was removed.
6. **A broken test:** the first `API_28_Unknown_user_gets_401` theory concatenated its inputs, and one case (`"HANDLER.ALEX "`) would have been a *valid* user (the
   username is trimmed, and SQL Server's default collation is case-insensitive). Rewritten before it produced a misleading result.
7. **Deprecated API:** used the parameterless `MsSqlBuilder()`, which is obsolete in Testcontainers 4.15 (an error under warnings-as-errors). Fixed.
8. **Naming clash:** `DependencyInjection` in both API and Application made `Program.cs` ambiguous. Constants moved to `CorsPolicies` and `HealthCheckTags`.
9. **The first mutation check was invalid:** replacing `timeProvider.GetUtcNow()` left the parameter unused, so the *compiler* failed, not the architecture test.
   Redone by adding `_ = DateTimeOffset.UtcNow;` instead; `CONV_16` then failed and named the method. The mutation was reverted.

## 7. What Vlad changed or rejected in review
_To be filled in from Vlad's review of this phase. Do not invent entries._
