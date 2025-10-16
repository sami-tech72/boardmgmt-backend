import { CanMatchFn, Route, UrlSegment, Router } from '@angular/router';
import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { MeService } from '../services/me.service';
import { AccessService } from '../services/access.service';
import { AppModule, Permission } from '../models/security.models';
import { AuthService } from '../services/auth.service';

type PermData = { module: AppModule; require?: Permission };

export const permGuard: CanMatchFn = async (route: Route, _segments: UrlSegment[]) => {
  const router = inject(Router);
  const me = inject(MeService);
  const access = inject(AccessService);
  const auth = inject(AuthService);
  const platformId = inject(PLATFORM_ID);

  if (!isPlatformBrowser(platformId)) return true;

  // Not authenticated → go login with returnUrl
  if (!auth.isAuthenticated) {
    const returnUrl = router.url || '/';
    return router.createUrlTree(['/auth'], { queryParams: { returnUrl } });
  }

  const okLoaded = await me.ensureLoaded();
  if (!okLoaded) {
    const returnUrl = router.url || '/';
    return router.createUrlTree(['/auth'], { queryParams: { returnUrl } });
  }

  const data = (route.data ?? {}) as PermData;
  if (!data.module) return true;

  const need = data.require ?? Permission.View;
  const ok = access.can(data.module, need);
  return ok ? true : router.createUrlTree(['/unauthorized']);
};
