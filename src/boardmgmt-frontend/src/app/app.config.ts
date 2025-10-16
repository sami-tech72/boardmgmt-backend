import { ApplicationConfig } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from '@core/interceptors/auth.interceptor';
import { apiEnvelopeInterceptor } from '@core/interceptors/api-envelope.interceptor';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideToastr } from 'ngx-toastr';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideAnimations(),
    provideToastr({
      positionClass: 'toast-top-right',
      preventDuplicates: true,
      timeOut: 4000,
      closeButton: true,
    }),
    // Order: auth → envelope (attach token, then unwrap/handle)
    provideHttpClient(withInterceptors([authInterceptor, apiEnvelopeInterceptor])),
  ],
};
