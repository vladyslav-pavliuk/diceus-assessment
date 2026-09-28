import { environment } from '../../../environments/environment';

/** Segments are URI-encoded. */
export function apiUrl(...segments: string[]): string {
  return `${environment.apiBaseUrl}/api/${segments.map(encodeURIComponent).join('/')}`;
}

/** The interceptors leave every other host alone. */
export function isApiRequest(url: string): boolean {
  return url.startsWith(`${environment.apiBaseUrl}/api/`);
}
