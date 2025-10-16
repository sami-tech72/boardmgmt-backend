import { Routes } from '@angular/router';

export const MEETINGS_CALENDAR_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./calendar.page').then((m) => m.CalendarPage),
  },
];
