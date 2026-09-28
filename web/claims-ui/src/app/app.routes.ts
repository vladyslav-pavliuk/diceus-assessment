import { Routes } from '@angular/router';

// 'claims/new' comes before 'claims/:id', so "new" is never read as a claim id.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'claims' },
  {
    path: 'claims',
    pathMatch: 'full',
    loadChildren: () => import('./features/claims-list/claims-list.routes'),
  },
  {
    path: 'claims/new',
    loadChildren: () => import('./features/fnol-intake/fnol-intake.routes'),
  },
  {
    path: 'claims/:id',
    loadChildren: () => import('./features/claim-detail/claim-detail.routes'),
  },
  { path: '**', redirectTo: 'claims' },
];
