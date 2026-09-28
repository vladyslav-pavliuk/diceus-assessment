import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { USER_ROLES } from '../models/enums';
import { User } from '../models/reference.models';
import { NotificationService } from '../notify/notification.service';

/**
 * The signed-in user and role (FRS §11.4 "UI shows logged-in user name and role in header") and the
 * role switcher for testing (FRS §11.4, D-16). Switching reloads the current screen, so every
 * role-gated control is recomputed from the new user.
 */
@Component({
  selector: 'app-user-switcher',
  imports: [MatButtonModule, MatMenuModule, MatIconModule, MatDividerModule],
  template: `
    <button
      mat-button
      class="switcher"
      [matMenuTriggerFor]="menu"
      (menuOpened)="loadUsers()"
      [disabled]="switching()"
      aria-label="Switch user"
    >
      <mat-icon>account_circle</mat-icon>
      @if (auth.user(); as user) {
        <span class="switcher__name">{{ user.displayName }}</span>
        <span class="switcher__role">{{ user.role }}</span>
      } @else {
        <span class="switcher__name">Not signed in</span>
      }
      <mat-icon iconPositionEnd>arrow_drop_down</mat-icon>
    </button>
    <mat-menu #menu="matMenu" xPosition="before">
      @if (users().length === 0) {
        <button mat-menu-item disabled>Loading users…</button>
      }
      @for (group of groups(); track group.role; let last = $last) {
        <div class="switcher__group" role="presentation">{{ group.role }}</div>
        @for (user of group.users; track user.id) {
          <button mat-menu-item (click)="switchTo(user)" [disabled]="user.id === auth.userId()">
            <mat-icon>{{ user.id === auth.userId() ? 'check' : 'person' }}</mat-icon>
            <span>{{ user.displayName }}</span>
          </button>
        }
        @if (!last) {
          <mat-divider />
        }
      }
    </mat-menu>
  `,
  styles: `
    .switcher {
      color: var(--mat-sys-on-primary);
    }
    .switcher__name {
      margin-left: 4px;
    }
    .switcher__role {
      margin-left: 8px;
      padding: 2px 8px;
      border-radius: 999px;
      background: var(--mat-sys-tertiary-fixed-dim);
      color: var(--mat-sys-on-tertiary-fixed);
      font: var(--mat-sys-label-small);
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .switcher__group {
      padding: 8px 16px 4px;
      font: var(--mat-sys-label-small);
      color: var(--mat-sys-on-surface-variant);
      text-transform: uppercase;
    }
  `,
})
export class UserSwitcher {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  protected readonly users = signal<User[]>([]);
  protected readonly switching = signal(false);
  protected readonly groups = computed(() =>
    USER_ROLES.map((role) => ({
      role,
      users: this.users().filter((user) => user.role === role),
    })).filter((group) => group.users.length > 0),
  );

  protected loadUsers(): void {
    if (this.users().length > 0) {
      return;
    }
    this.auth.demoUsers().subscribe((users) => this.users.set(users));
  }

  protected switchTo(user: User): void {
    this.switching.set(true);
    this.auth
      .signIn(user.username)
      .pipe(finalize(() => this.switching.set(false)))
      .subscribe(() => {
        this.notifications.info(`Signed in as ${user.displayName} (${user.role}).`);
        this.reloadCurrentScreen();
      });
  }

  /** Re-creates the routed component so its role-dependent state is rebuilt for the new user. */
  private reloadCurrentScreen(): void {
    const url = this.router.url;
    void this.router
      .navigateByUrl('/', { skipLocationChange: true })
      .then(() => this.router.navigateByUrl(url));
  }
}
