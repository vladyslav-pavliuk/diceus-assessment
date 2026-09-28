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

/** Switching user reloads the current screen, so every role-gated control is recomputed (D-16). */
@Component({
  selector: 'app-user-switcher',
  imports: [MatButtonModule, MatMenuModule, MatIconModule, MatDividerModule],
  templateUrl: './user-switcher.html',
  styleUrl: './user-switcher.scss',
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

  private reloadCurrentScreen(): void {
    const url = this.router.url;
    void this.router
      .navigateByUrl('/', { skipLocationChange: true })
      .then(() => this.router.navigateByUrl(url));
  }
}
