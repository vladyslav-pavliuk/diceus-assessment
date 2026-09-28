# AI log: Refinement phase (backend and frontend clean-up)

- **Date:** 2026-09-28
- **Tool:** Claude Code (desktop app), model Claude Opus 5.5.
- **Scope:** the .NET backend (`src/**`), then the Angular app (`web/claims-ui/src/**`), then merging and pruning branches, then one UI spacing fix.
- **Branches:** `refactor/backend-cleanup`, `refactor/frontend-cleanup` and `fix/tab-actions-spacing`, each merged into `main` with `--no-ff` and deleted.

## 1. Prompts given (verbatim)

### Prompt 1: backend

```
We need to refactor the solution.
Let's start from the .NET backend.

* Remove comments in places where code is self-explained and make <summary> blocks shorter to point on significant details only.
* ApiProblems class name is inappropriate for an enterprise project.
* ClaimAuditTrail is unused, any reasons for that? + Check fir any other unused imports, classes, properties etc., but be careful if they are unused for purpose like validator classes.
```

### Prompt 2: frontend, merging, branches, this log

```
okay, you can merge this branch to main and push and let's create a new one for the frontend.
the fronted mostly looks clean, the only things I don't like are:

* for some components you put the template straight into '@Component' decorator, I prefer separate files for that.
* same as for the BE, remove comments in places where code is self-explained and keep only significant ones.


then merge fe feature branch to main too, remove all other branches which were merged to main too (local and remote).

log this conversation to docs/ai-logs as phase-refinement.
```

### Prompts 3–5: UI fix, push, this log entry

```
one small ui issue, please add top margin for the button.
```

(with a screenshot of the claim detail's **Link policy** button touching the policy search field above it)

```
yes, push it
```

```
add this fix to the phase-refinement log
```

## 2. What was generated: backend

- **Unused code.** Found in three ways: a one-off analyzer build with IDE0005/IDE0051/IDE0052/IDE0059/IDE0060/IDE0035/CA1823 raised to warnings;
  a script listing every `src` type, member, constant, interface member and enum value that nothing references by name; and a check of each project's
  package references. Removed: 6 unused `using` directives (1 in `src`, 5 in tests), the unread `_otherSupervisor` field in `ReserveEndpointTests`
  (it signed in a user that no test used), and the `Microsoft.Extensions.Options` reference in `ClaimsModule.Application.csproj` (no `IOptions` use
  since Phase 1). Its central `PackageVersion` stays, because transitive pinning is enabled.
- **Kept on purpose:** every type the scan flagged but the framework finds at runtime: controllers, MediatR handlers, FluentValidation validators,
  EF configurations, AutoMapper profiles, options classes, and the domain-event handlers `ClaimAuditTrail` and `GlPostingEnqueuer`. Also kept:
  the enum values `DocumentType.MedicalReport` and `ClaimSeverity.Catastrophic` (FRS reference values that clients send by name).
- **`ClaimAuditTrail` question.** It is not unused. `AddDomainEventHandlers` registers it by assembly scan under 21 `IBeforeCommitHandler<T>`
  interfaces, and it writes every audit row. The IDE marks it unused because nothing names it. Its summary now says so, and the test
  `AUD_I1_Every_domain_event_has_a_before_commit_audit_handler` fails if a registration is missing.
- **Rename.** `ApiProblems` became `ErrorResponseFactory`. `ProblemDetailsFactory` was not used, because it would shadow ASP.NET Core's
  `Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory`.
- **Comments.** Trimmed layer by layer, one commit per project. Removed: summaries that restate the member name, route or audit event; bare "FRS §x"
  labels on enums and DTOs; references to internal documents (`CLAUDE.md rule N`, `ARCHITECTURE-PLAN §x`, `Phase N`, `REVIEW-PREP`, a personal name
  and date). Kept, shortened: the reasons behind concurrency, transaction ordering, security and FRS/decision choices, with their rule and D-xx IDs.
- **Safety net.** A script strips every comment and blank line from HEAD and from the working tree and compares the two. It passed after every layer,
  so no code changed except the unused items above. An XML-doc build with CS1574 enabled confirmed that every remaining `cref` resolves.
- **Docs.** 255 `File.cs:line` references in `REQUIREMENTS-MATRIX.md`, `REVIEW-PREP.md` and `DECISIONS.md` were remapped with a line diff against
  `381f7e2`. A check confirmed that each one lands on a code line. The historical `ai-log/phase-*.md` files were left as they were.

## 3. What was generated: frontend

- **Inline templates.** 13 components had `template:` in `@Component`, and 9 of them also had inline `styles:`. A script moved each literal verbatim
  (dedented) into `name.html` / `name.scss` beside the component and replaced it with `templateUrl` / `styleUrl`, as the other 16 components already
  do. None of the literals contained `${…}` or escapes, so the move was lossless. Prettier formatted the new files. The styles were not in the
  request; they were moved as well for consistency, because every other component keeps its styles in a `.scss` file.
- **Comments.** About 450 comment lines went down to 219. Removed: route and DTO-name labels ("GET /api/claims (ClaimSummaryDto)"), "Tab N (FRS §11.3)"
  descriptions of what a template shows, `// --- Section ---` dividers in the API service, lazy-loading notes on route files, and references to
  internal documents (`CLAUDE.md rule N`, `Phase 8 finding F3`, "brief §x", "D-xx item N"). Kept, shortened: which backend rule a UI copy mirrors
  (`Mirrors Claim.WouldExceedAggregateLimit`), the reason behind a timing or browser workaround (window.open before the async call, the two-picker
  loss date), and rule/decision IDs. The generated `_theme-colors.scss` and test names were not touched.
- **Safety net.** The same approach as the backend: comments and blank lines stripped from HEAD and the working tree, then compared. It passed.
- **Docs.** 40 frontend `file:line` references in `REQUIREMENTS-MATRIX.md` and `REVIEW-PREP.md` were remapped against `d208561` (whitespace-insensitive,
  following a reference into the new `.html`/`.scss` when it cited an inline template). The three large jumps were checked by hand: two cited a class
  that moved up once its template left the file, and one cited the old header comment of `styles.scss`.

## 4. Merging and branches

- `refactor/backend-cleanup` → `main` (merge commit `d208561`), pushed. `refactor/frontend-cleanup` → `main` (`d63661f`), pushed. Pushing `main` runs CI
  only; deployment stays a manual `workflow_dispatch`.
- `git branch --merged main` listed every local branch, so all seven were deleted with `git branch -d` (which refuses unmerged work):
  `phase-1-skeleton`, `phase-5-documents`, `phase-6-frontend`, `phase-7-azure`, `phase-8-review`, `refactor/backend-cleanup`, `refactor/frontend-cleanup`.
  The remote had only `main`, so there was no remote branch to delete.

## 5. Follow-up: Link policy button spacing

- **Cause, measured in the running app** (the dev server on :4200 with the Docker API): the `.tab__actions` row started at exactly the pixel where
  the policy field's outline ended. The field's hint/error area rendered 0 px tall, and the `app-policy-typeahead` host is `display: inline`, so
  nothing separated them. The notes textarea and its **Save notes** / **Discard** row (the only other `.tab__actions` use) had the same zero gap.
- **Fix:** `margin-top: 12px` on the shared `.tab__actions` rule in `tabs.scss`, so both rows are fixed. After hot reload the measured gap was 12 px,
  and a screenshot confirmed it. Pre-existing since `fd3a78a` (Phase 6), not caused by this refactor.
- **Delivery:** `fix/tab-actions-spacing` → `main` (`4cbb1ed`), branch deleted. The push waited for Vlad's explicit "yes, push it".

## 6. Verification

- `dotnet build`: 0 warnings, 0 errors (warnings are errors).
- `dotnet test`: **695/695** (326 domain, 89 application, 280 integration).
- `ng build`: no warnings. `ng test`: **100/100** (11 files). `ng lint`: all files pass.
- After the spacing fix: `ng build` clean (a style-only change).

## 7. What the AI got wrong

1. **Ran the tests on a runtime that is not installed, again.** The first `dotnet test` aborted every test host, the same mistake as in Phase 8.
   It was re-run with `DOTNET_ROLL_FORWARD=Major`.
2. **First remap of the doc line references was imprecise.** A reference whose cited line was a deleted summary sometimes landed on a bare
   `/// </summary>` line, and a line changed by the rename landed on a blank line. The script was fixed to diff against the renamed text and to
   move forward to the next code line. It was re-run from clean docs, and the JOB-09 reference was pointed at the
   `[DisableConcurrentExecution]` attribute by hand.
3. **Trimmed one comment too far.** In `party-form.ts`, "the name rules read the type: re-check them whenever it changes" was first cut to "the name
   rules read the type", which loses the point. Restored before committing.
4. **Missed the spacing bug.** The frontend pass checked builds, tests and lint but no rendered screens, so the button touching the field above it
   was found by Vlad, not by the AI.

## 8. What Vlad changed or rejected

- After reviewing the backend refactor, Vlad accepted it as delivered (no changes) and asked for it to be merged and pushed.
- For the frontend, Vlad set the direction: templates in separate files, and the same comment policy as the backend.
- Vlad found the Link policy spacing issue by eye and asked for a top margin; the fix went in as asked.
