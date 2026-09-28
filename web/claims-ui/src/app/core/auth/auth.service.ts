import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, firstValueFrom, map, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { apiUrl } from '../api/api-url';
import { CLOCK } from '../clock';
import { DevTokenResponse, User } from '../models/reference.models';

interface Session {
  accessToken: string;
  expiresAt: string;
  user: User;
}

const STORAGE_KEY = 'claims-ui.session';

/**
 * Mock authentication (D-16): the API issues and validates a real JWT; this service only picks the user. The session
 * lives in sessionStorage, so each browser tab can be a different user.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly now = inject(CLOCK);
  private readonly session = signal<Session | null>(null);

  readonly user = computed(() => this.session()?.user ?? null);
  readonly role = computed(() => this.session()?.user.role ?? null);
  readonly userId = computed(() => this.session()?.user.id ?? null);
  readonly isSignedIn = computed(() => this.session() !== null);

  /** Null when there is no session or it has expired. */
  accessToken(): string | null {
    const current = this.session();
    if (!current || new Date(current.expiresAt) <= this.now()) {
      return null;
    }
    return current.accessToken;
  }

  /** Restores the tab's session or signs in as the default demo user. Never throws. */
  async restore(): Promise<void> {
    const stored = readStoredSession();
    if (stored && new Date(stored.expiresAt) > this.now()) {
      this.session.set(stored);
      return;
    }

    try {
      await firstValueFrom(this.signIn(environment.defaultUsername));
    } catch {
      // The API may be cold-starting (D-36); the shell then shows "Not signed in".
      this.clear();
    }
  }

  signIn(username: string): Observable<User> {
    return this.http.post<DevTokenResponse>(apiUrl('auth', 'dev-token'), { username }).pipe(
      tap((response) =>
        this.store({
          accessToken: response.accessToken,
          expiresAt: response.expiresAt,
          user: response.user,
        }),
      ),
      map((response) => response.user),
    );
  }

  demoUsers(): Observable<User[]> {
    return this.http.get<User[]>(apiUrl('auth', 'users'));
  }

  /** Called on a 401. */
  clear(): void {
    this.session.set(null);
    try {
      sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage may be unavailable (private mode); the in-memory session is already gone.
    }
  }

  private store(session: Session): void {
    this.session.set(session);
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      // Without storage the session still works for this page load.
    }
  }
}

function readStoredSession(): Session | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as Session) : null;
  } catch {
    return null;
  }
}
