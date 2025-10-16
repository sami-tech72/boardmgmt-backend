import { Routes } from '@angular/router';

export const MEETINGS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./meetings.page').then((m) => m.MeetingsPage),
  },
  {
    path: 'calendar',
    loadComponent: () => import('./calendar/calendar.page').then((m) => m.CalendarPage),
  },
  {
    path: 'calender',
    pathMatch: 'full',
    redirectTo: 'calendar',
  },
];
