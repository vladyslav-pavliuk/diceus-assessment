import { Routes } from '@angular/router';
import { FnolIntake } from './fnol-intake';

// Lazily loaded by app.routes.ts: the fnol-intake chunk (D-17).
export default [
  {
    path: '',
    component: FnolIntake,
    title: 'Log new claim · DICEUS',
    // Leaving a half-filled FNOL asks first.
    canDeactivate: [(component: FnolIntake) => component.canLeave()],
  },
] satisfies Routes;
