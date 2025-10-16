import {
  HttpInterceptorFn,
  HttpResponse,
  HttpErrorResponse,
  HttpContextToken,
  HttpContext,
} from '@angular/common/http';
import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { map, throwError, catchError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export type ApiOk<T> = { success: true; data: T; message?: string };
export type ApiErr = { success: false; error: { code?: string; message: string; details?: any }; traceId?: string };

export const API_ENVELOPE = new HttpContextToken<boolean>(() => true);

/** Use this to suppress toasts for "background/silent" calls like permission probes */
export const QUIET = new HttpContextToken<boolean>(() => false);

const isMutation = (m: string) => /^(POST|PUT|PATCH|DELETE)$/i.test(m);

export const apiEnvelopeInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.context.get(API_ENVELOPE)) return next(req);

  const toastr = inject(ToastrService);
  const platformId = inject(PLATFORM_ID);
  const canToast = isPlatformBrowser(platformId) && !req.context.get(QUIET);
  const router = inject(Router);
  const auth = inject(AuthService);

  return next(req).pipe(
    map((evt) => {
      if (evt instanceof HttpResponse) {
        const body = evt.body;
        if (body && typeof body === 'object' && 'success' in body) {
          if ((body as ApiOk<unknown>).success === true) {
            const ok = body as ApiOk<unknown>;
            if (canToast && isMutation(req.method) && ok.message) toastr.success(ok.message);
            return evt.clone({ body: ok.data ?? body });
          }
        }
      }
      return evt;
    }),
    catchError((err: HttpErrorResponse) => {
      const apiErr = err?.error as ApiErr | undefined;
      const message = apiErr?.error?.message ?? err?.message ?? 'Something went wrong. Please try again.';

      // 401 handling: silent logout, redirect to /auth with returnUrl (no toast)
      if (err.status === 401 && isPlatformBrowser(platformId)) {
        // If we were authenticated, drop the token
        if (auth.isAuthenticated) auth.logout();

        // Build returnUrl only for navigations not already at /auth
        const current = router.url || '/';
        const alreadyAuth = current.startsWith('/auth');
        const tree = alreadyAuth ? ['/auth'] : ['/auth'], query = alreadyAuth ? {} : { returnUrl: current };
        router.navigate(tree, { queryParams: query });
        // Do not toast on 401 to avoid noise
        return throwError(() => err);
      }

      if (canToast) {
        const trace = apiErr?.traceId ? ` (Trace: ${apiErr.traceId})` : '';
        toastr.error(`${message}${trace}`);
      }

      // propagate a cleaner error
      return throwError(() => ({
        ...err,
        message,
        traceId: apiErr?.traceId,
        code: apiErr?.error?.code,
        details: apiErr?.error?.details,
      }));
    }),
  );
};
