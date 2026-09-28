# AI log: Phase 9 (backend refactor: comments, naming, unused code)

- **Date:** 2026-09-28
- **Tool:** Claude Code (desktop app), model Claude Opus 5.5.
- **Scope:** `src/**` (.NET backend) only. The Angular app is the next step.

## 1. Prompt given (verbatim)

```
We need to refactor the solution.
Let's start from the .NET backend.

* Remove comments in places where code is self-explained and make <summary> blocks shorter to point on significant details only.
* ApiProblems class name is inappropriate for an enterprise project.
* ClaimAuditTrail is unused, any reasons for that? + Check fir any other unused imports, classes, properties etc., but be careful if they are unused for purpose like validator classes.
```

## 2. What was generated

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

## 3. Verification

- `dotnet build`: 0 warnings, 0 errors (warnings are errors).
- `dotnet test`: **695/695** (326 domain, 89 application, 280 integration).

## 4. What the AI got wrong

1. **Ran the tests on a runtime that is not installed, again.** The first `dotnet test` aborted every test host, the same mistake as in Phase 8.
   It was re-run with `DOTNET_ROLL_FORWARD=Major`.
2. **First remap of the doc line references was imprecise.** A reference whose cited line was a deleted summary sometimes landed on a bare
   `/// </summary>` line, and a line changed by the rename landed on a blank line. The script was fixed to diff against the renamed text and to
   move forward to the next code line. It was re-run from clean docs, and the JOB-09 reference was pointed at the
   `[DisableConcurrentExecution]` attribute by hand.

## 5. What Vlad changed or rejected

- _(Pending review.)_
