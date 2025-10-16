import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { BehaviorSubject, tap } from 'rxjs';
import { environment } from '@env/environment';
import { BROWSER_STORAGE } from '../tokens/browser-storage.token';
import { isExpired, decodeJwt, DecodedJwt } from '../utils/jwt.util';
import { API_ENVELOPE } from '../interceptors/api-envelope.interceptor';
import { AccessService } from './access.service';

export interface AuthState {
  token: string | null;
  isAuthenticated: boolean;
  user?: { id?: string; email?: string; [k: string]: any } | null;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private storage = inject(BROWSER_STORAGE);
  private access = inject(AccessService);

  private state$ = new BehaviorSubject<AuthState>(this.computeState(this.storage.getItem('jwt')));
  stateChanges = this.state$.asObservable();
  snapshot = () => this.state$.value;

  private computeState(token: string | null): AuthState {
    if (!token || isExpired(token)) return { token: null, isAuthenticated: false, user: null };
    const payload: DecodedJwt | null = decodeJwt(token);
    return {
      token,
      isAuthenticated: true,
      user: payload?.sub ? { id: payload.sub, email: payload.email } : null,
    };
  }

  get token(): string | null { return this.state$.value.token; }
  get isAuthenticated(): boolean { return this.state$.value.isAuthenticated; }

  /** After envelope unwrap, body is { token: string } */
  login(email: string, password: string) {
    return this.http
      .post<{ token: string }>(
        `${environment.apiUrl}/auth/login`,
        { email, password },
        { context: new HttpContext().set(API_ENVELOPE, true) }
      )
      .pipe(tap(({ token }) => this.setToken(token)));
  }

  setToken(token: string) {
    if (!token || isExpired(token)) { this.clear(); return; }
    // Clear old perms immediately when user changes
    this.access.clear();

    this.storage.setItem('jwt', token);
    this.state$.next(this.computeState(token));
  }

  clear() {
    this.storage.removeItem('jwt');
    this.access.clear();
    this.state$.next({ token: null, isAuthenticated: false, user: null });
  }

  logout() { this.clear(); }
}
