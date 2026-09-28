# AI log: Phase 5 (documents: storage abstraction, Azure Blob + local fallback, upload, SAS)

- **Date:** 2026-09-28 (one session).
- **Tool:** Claude Code (desktop app), model Claude Opus 5.5.
- **Context loaded:** `CLAUDE.md`, `docs/DECISIONS.md` (D-08, D-24, D-26, D-28, D-36–D-41 in full), `docs/PROMPTS.md`, `docs/REQUIREMENTS-MATRIX.md`,
  `docs/ai-log/phase-4.md`, FRS §3, §7.4, §9.7, §10, §11.3 Tab 4, §13, §14; brief §3.3.1, §3.6; the existing document code (`ClaimDocument`,
  `SanitisedFileName`, `IStorageService`), the unit of work, the idempotency filter, the API composition and the test fixtures.

## 1. Prompt given (verbatim)

The Phase 5 prompt from `docs/PROMPTS.md`:
```
Phase 5: documents (FRS §7.4, §13). IStorageService with AzureBlobStorageService (Azure.Storage.Blobs;
SAS with a 1h TTL; user-delegation SAS when using DefaultAzureCredential) and LocalFileSystemStorageService (a local
download URL served by a dev-only endpoint). Selected via Storage:Provider. Upload: multipart, 50 MB limit, MIME
allowlist checked by content sniffing (not just the Content-Type header), filename sanitisation against path
traversal (write tests with "../", absolute paths, and unicode tricks), DOCUMENT_UPLOADED audit. List returns
fresh SAS URLs. Integration-test the Azure implementation against Azurite. Stop and summarise.
```
Session events, for the record:
- Vlad interrupted the first attempt during exploration. At that moment `main` held only Phase 0 and `src/` held only build output. Vlad re-sent the same
  prompt; `main` then held Phases 1–4.
- Later Vlad wrote "Try again" after a response was cut off; the work continued from where it had stopped (the .NET 9 container run and the docs).

No other instructions were given.

## 2. Requirement IDs touched
BR-D-01, BR-D-02, BR-D-03; DOC-01..08 and new **DOC-09**; API-08, API-09, API-27; AUD-12, BR-A-02 (now complete); TR-13 (documents); API-IDEMP (forms).
Decisions applied: D-08, D-24, D-26, D-28, D-36; new **D-42** (Q1–Q3 open, 20 items PROPOSED); D-28 amended (NFKC); D-37 package rows.

## 3. What was generated

| Area | Content |
|---|---|
| Domain | `SanitisedFileName` hardened (unpaired surrogates, NFKC before separator stripping, format/bidi/zero-width removal, slash look-alikes); `DocumentFormat` (FRS §13 allowlist, canonical MIME types, declared-type aliases, inline vs attachment); `DocumentBlobPath` value object; `ClaimDocument.Create` / `Claim.AddDocument` take the path (a row cannot point into another claim's folder) and derive the content type; `DocumentUploaded` carries type, content type, size; `Claim.EnsureModifiable(status)` for early refusal |
| Application | `UploadClaimDocumentCommand` (+ validator; handler owns its unit of work via the new `IHandlesOwnUnitOfWork` marker: blob first, metadata second, compensating delete unless the row exists); `ListClaimDocumentsQuery`, `GetDocumentDownloadUrlQuery`; `DocumentContentInspector` (magic bytes, OOXML directory without macros, text = no binary bytes); `DocumentDownloads` (1-hour lifetime, headers from stored metadata); `IStorageService` gains `DownloadHeaders`; `IClaimQueries` gains status and document reads; audit NewValue extended |
| Persistence | `ClaimQueries`: `GetStatusAsync`, `ListDocumentsAsync`, `GetDocumentAsync`, `DocumentExistsAsync` (no schema change, no migration) |
| Infrastructure | `AzureBlobStorageService` (account-key SAS or user-delegation SAS with a cached key, read-only/one blob/1 h/HTTPS, response headers in the signature, `If-None-Match: *`, container created on first use); `LocalFileSystemStorageService` (HMAC-signed expiring token, per-process key, root containment, `CreateNew`); `StorageOptions` + validator (LocalFileSystem only in Development; exactly one Azure credential form); provider chosen at resolve time. Packages: Azure.Storage.Blobs 12.27.0, Azure.Identity 1.17.2 |
| API | `ClaimDocumentsController` (POST multipart, GET list, GET url); `RejectBodiesLargerThanAttribute` (413 before binding); dev-only `GET /api/local-files/{token}` (anonymous, nosniff); `IdempotencyFilter` hashes forms by content; appsettings (`Storage` section) and compose (`/home/app/uploads`, Azurite `--skipApiVersionCheck`) |
| Tests | Domain 324 (+55), Application 89 (+38), Integration 279 (+51): HTTP on the local provider (27 cases, including the two multipart idempotency tests and the 413), Azure service against Azurite (6), API host on Azure/Azurite (2), local provider in isolation (12), configuration (4). Fixtures: per-host uploads root, `FailingDocumentCommit` hook, Azurite started on first use, `AzureBlobApiFactory` |
| Docs | D-42 (+ D-28 amendment, D-37 rows), ARCHITECTURE-PLAN §5 and new §5.1 (upload flow), matrix (17 rows Implemented P5, new DOC-09), REVIEW-PREP Phase 5 (14 Q&A), this log |

## 4. Verification that ran
- Host (SDK 10, `DOTNET_ROLL_FORWARD=Major`): `dotnet build` 0 warnings; `dotnet test` **692/692** (324 domain, 89 application, 279 integration).
- `mcr.microsoft.com/dotnet/sdk:9.0` (SDK 9.0.318), Testcontainers through the host Docker socket: build with `-warnaserror`, 0 warnings, **691/691** (the run
  before the 413 test was added). The first container run had 7 Azurite failures, see §5 item 8.
- **Mutation checks** (each reverted and the full suite re-run afterwards):
  - Sniffing disabled in the handler: 3 tests fail (1 Application, 2 HTTP).
  - Compensating delete removed: 2 fail (`DOC_09_A_failed_commit_removes_the_blob`, the HTTP DOC-09 test).
  - Local root containment removed: 8 fail (the local path theory).
  - NFKC → NFC: 15 fail (5 domain, 10 integration).
- **Container smoke** (`docker compose --profile app`, fresh database `ClaimsModulePhase5` through an override file in the scratchpad, as in Phase 4):
  - Upload with file name `../../etc/Police Report.pdf` → 201, stored as `/home/app/uploads/{org}/{claim}/{docId}_Police Report.pdf` by uid 1654 (non-root).
  - Anonymous GET of the returned link → 200, identical bytes, `Content-Type: application/pdf`, `Content-Disposition: inline; filename="Police Report.pdf";
    filename*=UTF-8''Police%20Report.pdf`, `X-Content-Type-Options: nosniff`.
  - 30 MB file (above Kestrel's default ~28.6 MB) → 201; 50 MB + 1 byte → 422 "The file must not exceed 50 MB."; 60 MB body → first 422 (see §5 item 6),
    after the fix **413** `PayloadTooLarge`.
  - Audit: DOCUMENT_UPLOADED with RelatedEntityId = document id and NewValue `{documentName, documentType, contentType, fileSizeBytes}`.
  - Stack stopped afterwards (`down`, volumes kept).
- **Not verified here:** the user-delegation SAS path (managed identity). Azurite supports it only with OAuth over HTTPS; it is verified against the real
  storage account in Phase 7.

## 5. What Claude got wrong, and what changed

No correction came from Vlad in this session. These are Claude's own mistakes, found by the build, the tests, the smoke run or a re-read, and fixed before
the summary:

1. **A defect in Phase 3 code, found by a Phase 5 test.** `API_IDEMP_A_repeated_upload_with_the_same_key_stores_one_document` failed with 422: the
   Idempotency-Key filter hashed the raw body, and a multipart boundary is random per request, so a genuine browser retry was "a different request".
   Phase 3 only had JSON tests, so it went unnoticed. Forms are now hashed by content (D-42 item 18), with a test that a different file under the same key is
   still refused.
2. **A claim in D-42 that the smoke run disproved.** The first draft of D-42 item 6 said a body over 51 MB "is cut off by the server with 413". On real
   Kestrel it came back as **422 with an empty key and framework wording** ("Failed to read the request form. Request body too large…"): MVC form binding
   catches Kestrel's exception. Fixed in behaviour, not in the text: `RejectBodiesLargerThanAttribute` answers 413 from Content-Length before anything
   reads the body; new test `DOC_06_A_body_over_the_endpoint_limit_is_413`; D-42 item 6 rewritten.
3. **Test-isolation weakness exposed by a mutation.** With the containment check removed (mutation M3), the local provider really wrote `outside.txt` and a
   file at the root path into the shared temp folder; those files survived, and the restored code's next run failed. The test now gives each run its own
   sandbox around the root and asserts the sandbox is empty; the two stray files were deleted.
4. **Package conflict.** The newest Azure SDK (Azure.Core 1.53+) depends on Microsoft.Extensions 10.x; restore failed with NU1109 against the .NET 9 pins.
   Pinned Azure.Storage.Blobs 12.27.0 / Azure.Identity 1.17.2 (Azure.Core 1.50.0), recorded in D-37 and D-42 item 19.
5. **Test helper bug.** `UploadDocumentOkAsync` declared `application/pdf` for every file, so the `.csv`/`.txt` uploads were (correctly) refused as a
   declared-type mismatch. The helper now declares the type a browser would send for the extension.
6. **Test bugs from framework details.** `FakeTimeProvider` cannot move backwards (the expired-SAS test now uses its own clock two hours back); xUnit
   serialises `[InlineData]` strings, which turned a lone surrogate into U+FFFD (that case moved into a `[Fact]`); `BlobContainerClient.GetBlobs` in
   12.27 has no optional parameters (compile error).
7. **An EF translation slip, caught on re-read before running.** The first `ListDocumentsAsync` ordered *after* projecting into a positional record, which
   EF cannot translate; the ordering moved before the projection.
8. **Environment, not code.** In the .NET 9 container run, 7 Azurite tests failed with ContainerNotFound: the tests reached Azurite as
   `host.docker.internal`, and the Azure SDK uses path-style URLs (`/{account}/{container}`) only for IP addresses and `localhost`, so it took the account name
   for the container. Re-run with `TESTCONTAINERS_HOST_OVERRIDE` set to the IP: all green. The same quirk is why the compose `app` profile keeps documents on
   local disk rather than on the `azurite` host (D-42 item 12).
9. **Expected fallout.** `API_ERR_Unhandled_exception_gets_500_without_internal_details_outside_development` (Phase 1) started a Production host, which now
   refuses to start without Azure storage configuration; the test sets a service URI (the client is only created on first use).

Where Claude departed from written text, it did not decide silently: D-42 **Q1** (the upload handler owns its unit of work, against CLAUDE.md rule 4's
"every command"), **Q2** (NFKC instead of D-28's NFC) and **Q3** (`DocumentName` is the sanitised name, against FRS §9.7 "as uploaded") are open
questions for Vlad, implemented as recommended.

## 6. Open items for Vlad
- ~~Decide D-42 Q1–Q3.~~ Done, see §7. Review D-42 items 1–20 (PROPOSED), and the D-38 / D-40 / D-41 items still PROPOSED.
- Phase 7: storage account with container `claim-documents` (private), `Storage__AzureBlob__ServiceUri` on the Container App, and RBAC for the managed
  identity to write blobs and obtain user delegation keys (verify whether Storage Blob Data Contributor alone suffices); verify a user-delegation SAS end to end.
- The local `sqlserver-data` volume still holds the stale Phase 1 database (Phase 4 §5 item 5); the smoke runs use their own database names.

## 7. Vlad's decisions after the summary
Vlad's reply (verbatim):
```
Accept Q1, Q2 and Q3, merge to main
```
Applied: D-42 Q1 (blob first, handler owns its unit of work), Q2 (NFKC, D-28 amendment) and Q3 (sanitised `DocumentName`) marked ACCEPTED. No code
change was needed: all three were already implemented as recommended. CLAUDE.md was not changed: rule 4 still holds for the upload (one transaction, no
`SaveChanges` in the handler), and the exception is documented in D-42 Q1 and ARCHITECTURE-PLAN §5. The `phase-5-documents` branch was merged into `main`.
