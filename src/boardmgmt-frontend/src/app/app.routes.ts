import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';
import { permGuard } from '@core/guards/perm.guard';
import { AppModule, Permission } from '@core/models/security.models';

export const routes: Routes = [
  {
    path: 'auth',
    loadChildren: () => import('@features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('@layout/shell/shell.component').then((m) => m.ShellComponent),
    children: [
      {
        path: 'dashboard',
        // ⛳ Dashboard accessible to any authenticated user
        loadChildren: () => import('@features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      {
        path: 'meetings',
        canMatch: [permGuard],
        data: { module: AppModule.Meetings, require: Permission.View },
        loadChildren: () => import('@features/meetings/meetings.routes').then((m) => m.MEETINGS_ROUTES),
      },
      {
        path: 'calender',
        pathMatch: 'full',
        redirectTo: 'meetings/calendar',
      },
      {
        path: 'documents',
        canMatch: [permGuard],
        data: { module: AppModule.Documents, require: Permission.View },
        loadChildren: () => import('@features/documents/documents.routes').then((m) => m.DOCUMENTS_ROUTES),
      },
      {
        path: 'voting',
        canMatch: [permGuard],
        data: { module: AppModule.Votes, require: Permission.View },
        loadChildren: () => import('@features/voting/voting.routes').then((m) => m.VOTING_ROUTES),
      },
      {
        path: 'reports',
        canMatch: [permGuard],
        data: { module: AppModule.Reports, require: Permission.View },
        loadChildren: () => import('@features/reports/reports.routes').then((m) => m.REPORTS_ROUTES),
      },
      {
        path: 'messages',
        canMatch: [permGuard],
        data: { module: AppModule.Messages, require: Permission.View },
        loadChildren: () => import('@features/chat/chat.routes').then((m) => m.CHAT_ROUTES),
      },
      {
        path: 'users',
        canMatch: [permGuard],
        data: { module: AppModule.Users, require: Permission.View },
        loadChildren: () => import('@features/users/users.routes').then((m) => m.USERS_ROUTES),
      },
      {
        path: 'settings',
        canMatch: [permGuard],
        data: { module: AppModule.Settings, require: Permission.View },
        loadChildren: () => import('@features/settings/settings.routes').then((m) => m.SETTINGS_ROUTES),
      },
      {
        path: 'roles',
        canMatch: [permGuard],
        data: { module: AppModule.Settings, require: Permission.Update },
        loadChildren: () => import('@features/roles/roles.routes').then((m) => m.ROLES_ROUTES),
      },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
    ],
  },
  {
    path: 'unauthorized',
    loadChildren: () => import('@features/auth/unauthorized.routes').then((m) => m.UNAUTHORIZED_ROUTES),
  },
  { path: '**', redirectTo: 'dashboard' },
];
