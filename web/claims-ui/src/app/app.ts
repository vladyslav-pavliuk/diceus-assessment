import { Component, inject } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterOutlet } from '@angular/router';
import { LoadingService } from './core/loading/loading.service';
import { UserSwitcher } from './core/shell/user-switcher';

/** The shell: toolbar with the signed-in user (FRS §11.4), a global progress bar and the routed screen. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, MatToolbarModule, MatProgressBarModule, UserSwitcher],
  template: `
    <mat-toolbar class="app-toolbar">
      <a class="app-brand" routerLink="/claims">
        <span class="app-brand__mark" aria-hidden="true">◆</span>
        <span>DICEUS Claims</span>
      </a>
      <span class="app-toolbar__spacer"></span>
      <app-user-switcher />
    </mat-toolbar>
    <div class="app-progress" aria-hidden="true">
      @if (loading.busy()) {
        <mat-progress-bar mode="indeterminate" />
      }
    </div>
    <main class="app-main">
      <router-outlet />
    </main>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
    }
    .app-toolbar {
      position: sticky;
      top: 0;
      z-index: 10;
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
    }
    .app-brand {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      color: inherit;
      text-decoration: none;
      font: var(--mat-sys-title-large);
    }
    .app-brand__mark {
      color: var(--mat-sys-tertiary-fixed-dim);
    }
    .app-toolbar__spacer {
      flex: 1;
    }
    .app-progress {
      height: 4px;
      position: sticky;
      top: 64px;
      z-index: 10;
    }
    .app-main {
      flex: 1;
      width: 100%;
      max-width: 1440px;
      margin: 0 auto;
      padding: 16px 24px 48px;
      box-sizing: border-box;
    }
  `,
})
export class App {
  protected readonly loading = inject(LoadingService);
}
