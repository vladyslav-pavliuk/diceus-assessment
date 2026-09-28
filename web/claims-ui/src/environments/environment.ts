// Production build settings. Phase 7 (deployment) sets apiBaseUrl to the Container App's URL at build time.
export const environment = {
  production: true,
  apiBaseUrl: 'http://localhost:5080',
  /** The seeded user a new browser tab signs in as (D-16); the toolbar switcher changes it. */
  defaultUsername: 'handler.alex',
};
