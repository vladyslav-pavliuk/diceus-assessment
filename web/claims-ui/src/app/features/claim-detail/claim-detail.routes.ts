import { Routes } from '@angular/router';
import { ClaimDetailPage } from './claim-detail';

// Lazily loaded by app.routes.ts: the claim-detail chunk (D-17). `:id` and `?tab=` bind to the page's
// inputs (withComponentInputBinding).
export default [{ path: '', component: ClaimDetailPage, title: 'Claim · DICEUS' }] satisfies Routes;
