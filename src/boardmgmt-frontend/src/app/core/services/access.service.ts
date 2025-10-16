import { Injectable } from '@angular/core';
import { AppModule, Permission, MODULE_PATHS, PATH_TO_MODULE } from '../models/security.models';

@Injectable({ providedIn: 'root' })
export class AccessService {
  private perms: Record<number, number> = {}; // { moduleId: flags }

  setAll(map: Record<number, number>) { this.perms = map ?? {}; }
  clear() { this.perms = {}; }

  can(mod: AppModule, p: Permission) {
    const v = this.perms[mod] ?? 0;
    return (v & p) === p;
  }

  /** Pick the first route the user can View; fallback null */
  firstAllowedPath(): string | null {
    const order: AppModule[] = [
      AppModule.Meetings,
      AppModule.Documents,
      AppModule.Votes,
      AppModule.Reports,
      AppModule.Messages,
      AppModule.Users,
      AppModule.Settings,
      AppModule.Folders,
    ];
    for (const m of order) {
      if (this.can(m, Permission.View)) return `/${MODULE_PATHS[m]}`;
    }
    return null;
  }

  /** Check if a given URL is accessible (by first path segment) */
  canAccessPath(url: string): boolean {
    try {
      const u = url.startsWith('http') ? new URL(url) : new URL(url, 'http://x');
      const seg = u.pathname.split('/').filter(Boolean)[0] ?? '';
      if (!seg) return true; // root is fine
      const mod = PATH_TO_MODULE[seg];
      if (!mod) return true; // not mapped to a module; treat as public under auth shell
      return this.can(mod, Permission.View);
    } catch {
      return true;
    }
  }
}
