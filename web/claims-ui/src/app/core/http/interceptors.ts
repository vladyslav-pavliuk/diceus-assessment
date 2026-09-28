import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, finalize, throwError } from 'rxjs';
import { isApiRequest } from '../api/api-url';
import { AuthService } from '../auth/auth.service';
import { LoadingService } from '../loading/loading.service';
import { NotificationService, NotificationSeverity } from '../notify/notification.service';
import { ApiError } from './api-error';

/** Opt a request out of the global progress bar (the policy typeahead has its own spinner). */
export const SKIP_GLOBAL_LOADING = new HttpContextToken<boolean>(() => false);

/** Opt a request out of the error snackbar, for a caller that reports the error itself. */
export const SKIP_ERROR_SNACKBAR = new HttpContextToken<boolean>(() => false);

/**
 * Adds the Bearer token (FRS §11.4) and a new correlation id to every API call. The API accepts a
 * client correlation id only as a GUID in the "D" form (D-39 Q1), which is what randomUUID returns;
 * it stamps the id on every audit row the request writes (CLAUDE.md rule 6).
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isApiRequest(request.url)) {
    return next(request);
  }

  const token = inject(AuthService).accessToken();
  const headers: Record<string, string> = { 'X-Correlation-Id': crypto.randomUUID() };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }
  return next(request.clone({ setHeaders: headers }));
};

/** Drives the shell's progress bar while any API call is in flight (FRS §11.4 loading states). */
export const loadingInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isApiRequest(request.url) || request.context.get(SKIP_GLOBAL_LOADING)) {
    return next(request);
  }

  const loading = inject(LoadingService);
  loading.start();
  return next(request).pipe(finalize(() => loading.stop()));
};

/**
 * Every API error becomes an {@link ApiError} and a snackbar with a severity (FRS §11.4). The error is
 * still rethrown, so a form can put a 422's messages next to its controls and a store can reload
 * after a 409.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const notifications = inject(NotificationService);

  return next(request).pipe(
    catchError((response: unknown) => {
      if (!(response instanceof HttpErrorResponse)) {
        return throwError(() => response);
      }

      const error = ApiError.fromHttp(response);
      if (error.status === 401 && isApiRequest(request.url)) {
        auth.clear();
      }
      if (!request.context.get(SKIP_ERROR_SNACKBAR)) {
        notifications.show(snackbarMessage(error), severityOf(error.status));
      }
      return throwError(() => error);
    }),
  );
};

export function severityOf(status: number): NotificationSeverity {
  if (status === 422 || status === 409 || status === 413) {
    return 'warn';
  }
  return 'error';
}

function snackbarMessage(error: ApiError): string {
  if (!error.isValidation) {
    return error.title;
  }
  const [first, ...rest] = error.messages;
  return rest.length > 0 ? `${first} (+${rest.length} more)` : first;
}
