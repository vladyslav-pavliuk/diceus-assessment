// The API origin is a build-time constant: the deploy workflow sets it with ng build --define (D-44).
declare const API_BASE_URL: string;

export const environment = {
  production: true,
  apiBaseUrl: API_BASE_URL,
  /** The seeded user a new browser tab signs in as (D-16). */
  defaultUsername: 'handler.alex',
};
