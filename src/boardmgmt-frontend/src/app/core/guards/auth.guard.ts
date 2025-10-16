import { CanActivateFn, Router } from '@angular/router';
import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const platformId = inject(PLATFORM_ID);
  const router = inject(Router);
  const auth = inject(AuthService);

  if (!isPlatformBrowser(platformId)) return true;

  if (!auth.isAuthenticated) {
    const returnUrl = router.url || '/';
    return router.createUrlTree(['/auth'], { queryParams: { returnUrl } });
  }
  return true;
};
