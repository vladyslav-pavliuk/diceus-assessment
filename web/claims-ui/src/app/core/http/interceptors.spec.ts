import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import { LoadingService } from '../loading/loading.service';
import { ApiError } from './api-error';
import { authInterceptor, errorInterceptor, loadingInterceptor } from './interceptors';

const API = `${environment.apiBaseUrl}/api/claims`;
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

describe('HTTP interceptors', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  const snackBar = { open: vi.fn() };
  const auth = { accessToken: vi.fn(() => 'token-123'), clear: vi.fn() };

  beforeEach(() => {
    snackBar.open.mockClear();
    auth.clear.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([authInterceptor, loadingInterceptor, errorInterceptor]),
        ),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snackBar },
        { provide: AuthService, useValue: auth },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('UI_GEN_03_Interceptor_adds_bearer and a GUID correlation id to API calls', () => {
    http.get(API).subscribe();
    const request = backend.expectOne(API).request;
    expect(request.headers.get('Authorization')).toBe('Bearer token-123');
    expect(request.headers.get('X-Correlation-Id')).toMatch(GUID);
  });

  it('UI_GEN_03_Other_hosts_get_no_token (a SAS download URL, for example)', () => {
    http.get('https://storage.example/blob').subscribe();
    const request = backend.expectOne('https://storage.example/blob').request;
    expect(request.headers.has('Authorization')).toBe(false);
  });

  it('UI_GEN_05_Error_interceptor_shows_snackbar and rethrows the 422 errors dictionary', () => {
    let caught: unknown;
    http.post(API, {}).subscribe({ error: (error) => (caught = error) });

    backend.expectOne(API).flush(
      {
        type: 'ValidationError',
        title: 'One or more validation errors occurred.',
        status: 422,
        errors: {
          LossDate: ['Loss date cannot be in the future.'],
          ClaimParties: ['At least one Claimant party is required.'],
        },
      },
      { status: 422, statusText: 'Unprocessable Entity' },
    );

    expect(caught).toBeInstanceOf(ApiError);
    expect((caught as ApiError).errors['LossDate']).toEqual(['Loss date cannot be in the future.']);
    expect(snackBar.open).toHaveBeenCalledWith(
      'Loss date cannot be in the future. (+1 more)',
      'Dismiss',
      expect.objectContaining({ panelClass: ['app-snack', 'app-snack--warn'] }),
    );
  });

  it('UI_GEN_05_A_401_clears_the_session', () => {
    http.get(API).subscribe({ error: () => undefined });
    backend.expectOne(API).flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(auth.clear).toHaveBeenCalled();
    expect(snackBar.open.mock.calls[0][2].panelClass).toContain('app-snack--error');
  });

  it('UI_GEN_06_Loading_counter_rises_and_falls_with_the_call', () => {
    const loading = TestBed.inject(LoadingService);
    http.get(API).subscribe();
    expect(loading.busy()).toBe(true);
    backend.expectOne(API).flush([]);
    expect(loading.busy()).toBe(false);
  });
});
