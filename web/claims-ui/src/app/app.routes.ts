import { Routes } from '@angular/router';

// One lazily loaded route file per feature (FRS §11.4, D-17): each becomes its own chunk.
// 'claims/new' is listed before 'claims/:id' so "new" is never read as a claim id.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'claims' },
  {
    path: 'claims',
    pathMatch: 'full',
    loadChildren: () => import('./features/claims-list/claims-list.routes'),
  },
  { path: '**', redirectTo: 'claims' },
];
