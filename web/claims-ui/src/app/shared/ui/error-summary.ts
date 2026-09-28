import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** The list of server messages shown at the top of a form step or dialog (FRS §11.2). */
@Component({
  selector: 'app-error-summary',
  imports: [MatIconModule],
  template: `
    @if (messages().length > 0) {
      <div class="banner banner--error" role="alert">
        <mat-icon>error_outline</mat-icon>
        <div>
          <strong>{{ title() }}</strong>
          <ul>
            @for (message of messages(); track $index) {
              <li>{{ message }}</li>
            }
          </ul>
        </div>
      </div>
    }
  `,
})
export class ErrorSummary {
  readonly messages = input<readonly string[]>([]);
  readonly title = input('Please correct the following:');
}
