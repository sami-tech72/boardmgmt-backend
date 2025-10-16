import { Routes } from '@angular/router';

export const VOTING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./voting.page').then((m) => m.VotingPage),
  },
];
