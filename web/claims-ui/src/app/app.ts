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
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly loading = inject(LoadingService);
}
