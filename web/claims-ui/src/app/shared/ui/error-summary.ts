import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-error-summary',
  imports: [MatIconModule],
  templateUrl: './error-summary.html',
})
export class ErrorSummary {
  readonly messages = input<readonly string[]>([]);
  readonly title = input('Please correct the following:');
}
