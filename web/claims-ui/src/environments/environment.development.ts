// `ng serve` / development build: the API from `dotnet run` or docker compose (CORS allows :4200, D-38).
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5080',
  /** The seeded user a new browser tab signs in as (D-16); the toolbar switcher changes it. */
  defaultUsername: 'handler.alex',
};
