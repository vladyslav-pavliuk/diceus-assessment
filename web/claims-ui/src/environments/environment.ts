// Production build settings. The API origin is a build-time constant (D-44): angular.json defaults it to the local API,
// and the deploy workflow overrides it with the Container App's URL: ng build --define "API_BASE_URL='https://…'".
declare const API_BASE_URL: string;

export const environment = {
  production: true,
  apiBaseUrl: API_BASE_URL,
  /** The seeded user a new browser tab signs in as (D-16); the toolbar switcher changes it. */
  defaultUsername: 'handler.alex',
};
