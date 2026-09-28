# AI log: Phase 6 (Angular frontend)

- **Date:** 2026-09-28 (one session).
- **Tool:** Claude Code (desktop app), model Claude Opus 5.5, with the desktop app's built-in browser pane for checking the running UI.
- **Context loaded:** `CLAUDE.md`, `docs/PROMPTS.md`, FRS §8, §10.4, §11, §14.1; brief §3.7, §5.2 Phase 4; `docs/DECISIONS.md` (D-05–D-09, D-16–D-33,
  D-38, D-40–D-42 in full); `docs/REQUIREMENTS-MATRIX.md` §12; `docs/ai-log/phase-5.md`; every API controller and request contract, the Application DTOs,
  the domain enums, `ReserveAuthorityPolicy`, `ReserveLimits`, `DomainMessages`, `ErrorKeys`, `Claim.Status.cs` and `Claim.Reserves.cs`.

## 1. Prompts given (verbatim)

The Phase 6 prompt from `docs/PROMPTS.md`:
```
Phase 6: the Angular app in web/claims-ui (FRS §11, brief §3.7). Plan the component tree, routes, services,
and state first, and show me the plan before generating code.
- Standalone components with lazy-loaded route files: claims-list, fnol-intake, claim-detail. A typed
  ClaimsApiService, ReferenceApiService, and AuthService (the only places that use HttpClient). Interfaces
  mirror the backend DTOs exactly.
- A mock auth with a user/role switcher in the toolbar; an interceptor adds the Bearer token and the correlation
  id; an error interceptor maps ProblemDetails (incl. the 422 errors dictionary) to snackbars and form errors;
  a loading indicator on every async operation, with buttons disabled while pending.
- Claims List: server-side pagination, filters (status multi-select, loss-date range, handler, cause), colour-coded
  status badges exactly as in FRS §11.1, empty state, row click → detail, "Log New Claim".
- FNOL: a MatStepper with one FormGroup per step, each validated independently; policy typeahead (debounced,
  switchMap); an in-force badge that recomputes when the loss date or policy changes; the Unknown Policy toggle;
  a future-date validator; a 20-char counter; a FormArray for parties with a Claimant-required validator; a FormArray
  for risk objects with an advisory; a live authority indicator; a review summary; a confirm dialog on warnings;
  the success snackbar with the claim number → detail.
- Claim Detail: a header with a copyable claim number, a status chip, and a transition menu of valid next statuses
  (a pre-flight checklist for Closed); the 5 tabs per FRS §11.3, including reserve summary cards, a history table with
  +/- colouring, a slide-in Add Reserve panel, role-gated Approve/Reject, Retract for the submitter only, the GL badge
  with retry, document upload with progress, and a paged audit log.
- A custom Material M3 theme with its own primary palette. Usable at 1280px+. No console errors.
- Unit tests for the FNOL validators and the authority-indicator logic.
Stop after the plan, then again after each screen.
```

Claude answered with a plan only (component tree, routes, services, state, per-screen behaviour, tests, delivery order) and four questions: Angular major
(recommended 22), scope beyond the literal FRS §11.3 list (Link policy, Acknowledge, Assign handler, reserve-limit override), primary colour
(`#1B4F72`), and the handler filter over all users instead of `?role=handler`.

Vlad's reply (verbatim):
```
accepted, you can start implementation and don't stop after each screen, I'll check everything at once and refine if needed
```
So all four recommendations were taken, and the per-screen stops were dropped. No other instructions were given.

## 2. Requirement IDs touched
UI-LIST-01..07, UI-FNOL-01..14, UI-DET-01..13, UI-GEN-01..08, RSV-09, SEC-03; the FE side of BR-C-01/02/03/06/07, BR-R-01/02/03, BR-P-01,
CC-01..04, BR-ST-02/03/04. Decisions applied: D-05, D-06, D-07, D-08, D-09, D-16, D-17, D-18, D-19, D-22, D-24, D-25, D-26, D-29, D-32, D-33, D-40, D-42;
new **D-43** (Q1–Q3 open, 17 items PROPOSED); D-29 amended (handler filter).

## 3. What was generated

| Area | Content |
|---|---|
| Scaffold | `web/claims-ui`: Angular 22.2 (`ng new`: standalone, zoneless, Vitest, SCSS), Angular Material/CDK 22.2, angular-eslint 22.5; environments with `fileReplacements`; production budgets raised to 800 kB / 1.5 MB |
| Theme | `styles/_theme-colors.scss` generated from `#1B4F72` (tertiary `#B8860B`); `styles.scss` with `mat.theme()`, the FRS §11.1 status tokens, banners, severity snackbars |
| Core | `models/` (DTO mirrors and enum unions), `ClaimsApiService`, `ReferenceApiService`, `AuthService` (dev token, sessionStorage, auto sign-in), `auth`/`loading`/`error` interceptors, `ApiError`, `NotificationService`, `LoadingService`, `CLOCK`, the toolbar `UserSwitcher` |
| Shared | `domain/` (authority, policy period, intake warnings, transitions + pre-flight, reserve row actions, Add Reserve rules), `forms/` (validators with the API's messages, 422 mapping, party / risk-object inline forms, policy typeahead), `ui/` (status chip, badge + tones, authority indicator, amount, confirm and prompt dialogs, error summary, empty state, file-size pipe) |
| Claims list | URL-driven query, filter bar (search, status multi-select, loss-date range, handler autocomplete, grouped cause select), table with SLA icon, skeleton, two empty states, paginator |
| FNOL | Typed form factory (three step groups, Unknown-policy and reserve cross-rules, date + time merge), three step components, linear stepper, warnings dialog, 422 placement per step, Idempotency-Key, unsaved-changes guard |
| Claim detail | Page-scoped `ClaimDetailStore` (reloads, 409 handling, bounded GL polling), header (copy, status chip, transition menu, assign dialog), transition dialog (reason, pre-flight, CC-04 justification, in-dialog 422), five tabs: overview (notes, severity, link policy, acknowledge), parties (+ risk objects), reserves (cards, aggregate meter, manager override, history, row actions, Add Reserve drawer), documents (upload with progress, signed-URL download), audit (paged, details, related-entity links) |
| Tests | 11 Vitest files, **95 tests**, named after matrix IDs: validators (22), authority (17), policy period + warnings, 422 mapping, transitions + CC checklist, reserve actions + Add Reserve rules, badge colours, FNOL form, tab rules, interceptors (HttpTestingController), claims list (URL mapping + rendered table / empty state) |
| Lint | `eslint.config.js` + `no-restricted-imports` on `HttpClient` outside the three services |
| Docs | D-43 (+ D-29 amendment), ARCHITECTURE-PLAN §9, REQUIREMENTS-MATRIX §12 + RSV-09 / SEC-03 (Implemented P6), REVIEW-PREP Phase 6 (12 Q&A), this log |

## 4. Verification that ran
- `ng build` (production): 0 errors, 0 warnings; initial 685 kB (154 kB transfer); lazy chunks `claims-list-routes`, `fnol-intake-routes`,
  `claim-detail-routes` (D-17 verified).
- `ng test`: **95/95** (Vitest 5, jsdom). `ng lint`: all files pass; a probe file importing `HttpClient` under `shared/` was rejected by the new rule,
  then deleted.
- **Mutation checks** (each reverted, suite re-run green): authority tier `<=` → `<` fails 2 tests (`RSV_09_…: 10000 → Auto`, `-10000 → Auto`);
  the Claimant validator accepting any role fails 2 tests (`UI_FNOL_09_…`).
- Backend unchanged, re-run anyway: `dotnet build` 0 errors; `dotnet test` **692/692** (324 domain, 89 application, 279 integration), host SDK 10 with
  `DOTNET_ROLL_FORWARD=Major`.
- **Browser run** against the real API (`docker compose --profile app`, fresh database `ClaimsModuleUi` through an override file in the scratchpad;
  `ng serve` at 1366×900), no console errors on any screen:
  - List: empty state, then one row; search `zzz` → URL `?q=zzz` and the filtered empty state.
  - FNOL as `handler.alex`: typeahead (`Harbor` → Harborview, amber badge; `POL-2025` → Coastal Builders, green), date + time, searchable cause, 49/20
    counter, Claimant party card, no risk object, Indemnity $25,000 → "⚠ Supervisor approval required", review table, warnings dialog ("No risk objects
    linked."), then **CLM-2026-0000001** created, snackbar "…The initial reserve awaits Supervisor approval.", navigated to the detail.
  - Detail as the handler: pending row with Retract, no Approve/Reject. Switched to `supervisor.casey`: Approve/Reject shown, approved → row Approved
    by Casey Morgan, GL badge **Pending → Posted** by the poll within ~6 s. Add Reserve on Indemnity −30,000 → "Reserve balance for Indemnity cannot go
    below zero."; Expense 5,000 → auto-approved and posted. Draft → Open through the pre-flight dialog (4 green items). Closed dialog: CC-01..03 green,
    CC-04 "Open reserves of $30,000.00 remain" + justification field (cancelled). Document uploaded (component driven with an in-page `File`), the signed
    local URL fetched: 200, `application/pdf`, `%PDF-1.4`. Audit tab: every event above, GL entries by "System", related-entity links.
- **Not verified here (be ready to say so):** the native file picker and a real new-tab download (the browser pane has no file chooser and does not open
  popups); reject, retract and GL retry clicked end to end (their visibility rules are unit-tested, the endpoints were integration-tested in Phase 4);
  the 409 reload path; the Idempotency-Key replay from the UI; reopen of a Closed claim; the Azure SAS download (Phase 7).

## 5. What Claude got wrong, and what changed

No correction came from Vlad in this session. These are Claude's own mistakes, found by the browser run, the build, lint, the tests or a re-read, and
fixed before the summary:

1. **The date and time pickers shared one FormControl.** Typing 1 Sep 2026 and 2:30 PM produced **28 Sep 2026 14:30** (today), which then failed "Loss date
   cannot be in the future". Angular writes a view change to the model only, not to the other accessor on the same control, so the time picker combined its
   time with today. Found only by driving the form in the browser; the unit tests could not see it. Fix: a separate `lossTime` control merged into
   `lossDate` without rewriting the date input's text (D-43 item 10), plus `UI_FNOL_05_The_time_picker_sets_the_time_of_the_picked_day`.
2. **An invented rule.** The first Overview tab limited claim notes to 4,000 characters. `Claims.Notes` is NVARCHAR(MAX) and the API has no limit;
   removed after checking the configuration (D-43 item 15).
3. **Double HTTP call.** `ClaimDetailStore.reloadReserves()` first both subscribed internally and returned the observable, so any caller that subscribed
   would fetch twice. Caught on re-read before running; it is now fire-and-forget.
4. **Store lifetime.** The plan said "route-scoped store"; a route provider lives in an injector that outlives the page (an old claim and a running poll
   would survive navigation). Provided by the page component instead, with the poll stopped on destroy.
5. **Tab content shifted sideways** after the Approve dialog closed. The closed Add Reserve drawer sits off-canvas, and restoring focus scrolled the
   `overflow: hidden` drawer container by 360 px (found with a DOM query for `scrollLeft > 0`). Fixed with `overflow: clip`. The drawer also overflowed its
   own width and hid the submit button; both are now sized explicitly.
6. **Claims list, first draft:** a template reference to a non-existent `state_loading()` and a "reload" implemented by adding a dummy query parameter.
   Replaced before the first build by a `reload$` subject combined with the query params.
7. **UX slips seen in screenshots:** the unfiltered empty list said "No claims match these filters" (now "No claims yet" with Log New Claim); small files
   showed "0 KB" (new `FileSizePipe`); the switcher's arrow sat before the name; "Expense add auto-approved." (now "Expense reserve opened and
   auto-approved."); a missing space before the aggregate percentage.
8. **Compile, lint and test errors:** indexing tone maps with the untyped `mat-table` row (TS7053, now typed helper methods); an icon projected into the
   wrong button slot (NG8011); `!=` in templates and unused destructured variables (lint); the list component test lacked a `DateAdapter` provider, and
   one of its expectations was a meaningless ternary, rewritten as a plain equality.
9. **Environment, not code:** the compose migrator failed with "There is already an object named 'Organisations'", because the local `sqlserver-data` volume
   still holds the stale Phase 1 database (known since Phase 4). The volume was left alone; the run used a separate database name through an override file.
   `ng serve` also stuck on an intermediate failed build while files were being created and needed a restart.

Where Claude departed from written text, it did not decide silently: D-43 **Q1** (the pre-flight checklist blocks Confirm), **Q2** (the "Reverse to zero"
option, not in the FRS §11.3 field list) and **Q3** (auto sign-in as `handler.alex`) are open for Vlad, implemented as recommended. The D-29 change was
decided by Vlad in the plan.

## 6. Open items for Vlad
- ~~Decide D-43 Q1–Q3.~~ Done, see §7. Review D-43 items 1–17 (PROPOSED).
- `.claude/launch.json` (the dev-server entry the desktop app's browser pane uses) is left uncommitted: decide whether it belongs in the repo.
- Phase 7: set `environment.ts` `apiBaseUrl` to the Container App URL at build time; add the Static Web App origin to `Cors:AllowedOrigins`.
- The local `sqlserver-data` volume still holds the stale Phase 1 database.

## 7. Vlad's decisions after the summary
Vlad's reply (verbatim):
```
Accept Q1, Q2 and Q3, merge to main
```
Applied: D-43 Q1 (the pre-flight checklist blocks Confirm), Q2 ("Reverse to zero" in the Add Reserve panel) and Q3 (auto sign-in as `handler.alex`)
marked ACCEPTED. No code change was needed: all three were already implemented as recommended. The `phase-6-frontend` branch was merged into `main`.
`.claude/launch.json` stays uncommitted (not decided).

