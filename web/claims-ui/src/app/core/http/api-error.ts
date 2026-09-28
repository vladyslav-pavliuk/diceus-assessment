import { HttpErrorResponse } from '@angular/common/http';

/** FRS §10.4. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** For a 422, `errors` is keyed by request property path, e.g. `Parties[0].FirstName` (D-40). */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly title: string,
    readonly errors: Readonly<Record<string, string[]>> = {},
    readonly type?: string,
  ) {
    super(title);
    this.name = 'ApiError';
  }

  get isValidation(): boolean {
    return this.status === 422;
  }

  /** The title when there are no field errors. */
  get messages(): string[] {
    const all = Object.values(this.errors).flat();
    return all.length > 0 ? all : [this.title];
  }

  static fromHttp(response: HttpErrorResponse): ApiError {
    if (response.status === 0) {
      return new ApiError(
        0,
        'The API cannot be reached. It may be starting up; try again in a moment.',
      );
    }

    const body = isProblemDetails(response.error) ? response.error : {};
    return new ApiError(
      response.status,
      body.title ?? defaultTitle(response.status),
      body.errors ?? {},
      body.type,
    );
  }
}

function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null && ('title' in value || 'errors' in value);
}

function defaultTitle(status: number): string {
  switch (status) {
    case 401:
      return 'Your session has expired. Choose a user to sign in again.';
    case 403:
      return 'You do not have permission to perform this action.';
    case 404:
      return 'The requested item was not found.';
    case 409:
      return 'Someone else changed this claim. The latest version has been loaded.';
    case 413:
      return 'The file is larger than 50 MB.';
    default:
      return status >= 500
        ? 'Something went wrong on the server. Please try again.'
        : 'The request failed.';
  }
}
