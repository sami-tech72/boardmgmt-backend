export enum Permission {
  View = 1,
  Create = 1 << 1,
  Update = 1 << 2,
  Delete = 1 << 3,
  Page = 1 << 4,
}

export enum AppModule {
  Users = 1,
  Meetings = 2,
  Documents = 3,
  Folders = 4,
  Votes = 5,
  Dashboard = 6,
  Settings = 7,
  Reports = 8,
  Messages = 9,
}

export const MODULES: { key: AppModule; title: string; icon: string }[] = [
  { key: AppModule.Dashboard, title: 'Dashboard', icon: 'fa-gauge' },
  { key: AppModule.Meetings, title: 'Meetings', icon: 'fa-calendar' },
  { key: AppModule.Documents, title: 'Documents', icon: 'fa-file' },
  { key: AppModule.Folders, title: 'Folders', icon: 'fa-folder' },
  { key: AppModule.Votes, title: 'Voting', icon: 'fa-square-check' },
  { key: AppModule.Reports, title: 'Reports', icon: 'fa-chart-column' },
  { key: AppModule.Messages, title: 'Messages', icon: 'fa-comments' },
  { key: AppModule.Users, title: 'Users', icon: 'fa-users' },
  { key: AppModule.Settings, title: 'Settings', icon: 'fa-gear' },
];

export const hasFlag = (v: number, f: Permission) => (v & f) === f;
export const toggleFlag = (v: number, f: Permission, on: boolean) => (on ? v | f : v & ~f);

/** Module ↔ path helpers for routing/permission-aware navigation */
export const MODULE_PATHS: Record<AppModule, string> = {
  [AppModule.Dashboard]: 'dashboard',
  [AppModule.Meetings]: 'meetings',
  [AppModule.Documents]: 'documents',
  [AppModule.Folders]: 'folders',
  [AppModule.Votes]: 'voting',
  [AppModule.Reports]: 'reports',
  [AppModule.Messages]: 'messages',
  [AppModule.Users]: 'users',
  [AppModule.Settings]: 'settings',
};

export const PATH_TO_MODULE = Object.fromEntries(
  Object.entries(MODULE_PATHS).map(([mod, path]) => [path, Number(mod) as AppModule])
) as Record<string, AppModule>;
