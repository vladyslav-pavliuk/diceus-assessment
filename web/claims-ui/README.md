# claims-ui

Angular 22 front end of the DICEUS Claims module (FRS §11, brief §3.7): claims list, FNOL intake, claim detail. Design notes: `docs/ARCHITECTURE-PLAN.md` §9
and `docs/DECISIONS.md` D-43.

## Run locally
1. Start the API on `http://localhost:5080` (`docker compose --profile app up -d` from the repository root, or `dotnet run` in `src/ClaimsModule.API`).
   CORS already allows `http://localhost:4200` in Development.
2. `npm ci`, then `npm start` and open http://localhost:4200.

A new browser tab signs in as `handler.alex`; switch user from the toolbar (seeded users, D-16). Each tab keeps its own user, so a handler can submit a
reserve in one tab and a supervisor approve it in another.

## Checks
- `npm test` — Vitest unit tests, named after the requirement IDs in `docs/REQUIREMENTS-MATRIX.md`.
- `npm run lint` — ESLint, including the rule that only the three API services may import `HttpClient`.
- `npm run build` — production build; the output lists one lazy chunk per feature.

The API URL comes from `src/environments/environment*.ts`.
