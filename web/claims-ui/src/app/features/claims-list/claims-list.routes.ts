import { Routes } from '@angular/router';
import { ClaimsList } from './claims-list';

// Lazily loaded by app.routes.ts: this file and everything it imports form the claims-list chunk (D-17).
export default [{ path: '', component: ClaimsList, title: 'Claims · DICEUS' }] satisfies Routes;
