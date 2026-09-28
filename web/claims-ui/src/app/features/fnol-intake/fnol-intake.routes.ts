import { Routes } from '@angular/router';
import { FnolIntake } from './fnol-intake';

export default [
  {
    path: '',
    component: FnolIntake,
    title: 'Log new claim · DICEUS',
    canDeactivate: [(component: FnolIntake) => component.canLeave()],
  },
] satisfies Routes;
