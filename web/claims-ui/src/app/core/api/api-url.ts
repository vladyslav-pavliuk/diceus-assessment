import { environment } from '../../../environments/environment';

/** Absolute URL of an API path, e.g. apiUrl('claims', id, 'reserves'). Segments are URI-encoded. */
export function apiUrl(...segments: string[]): string {
  return `${environment.apiBaseUrl}/api/${segments.map(encodeURIComponent).join('/')}`;
}

/** True for requests to our API (the interceptors leave every other host alone). */
export function isApiRequest(url: string): boolean {
  return url.startsWith(`${environment.apiBaseUrl}/api/`);
}
