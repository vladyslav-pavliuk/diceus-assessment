import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** The list of server messages shown at the top of a form step or dialog (FRS §11.2). */
@Component({
  selector: 'app-error-summary',
  imports: [MatIconModule],
  templateUrl: './error-summary.html',
})
export class ErrorSummary {
  readonly messages = input<readonly string[]>([]);
  readonly title = input('Please correct the following:');
}
