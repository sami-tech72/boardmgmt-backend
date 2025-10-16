import { Routes } from '@angular/router';

export const UNAUTHORIZED_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./login/unauthorized.page').then((m) => m.default),
  },
];
