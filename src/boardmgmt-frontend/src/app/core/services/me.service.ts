import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { BehaviorSubject, tap, firstValueFrom, throwError } from 'rxjs';
import { environment } from '@env/environment';
import { AccessService } from './access.service';
import { AuthService } from './auth.service';
import { QUIET, API_ENVELOPE } from '../interceptors/api-envelope.interceptor';

type PermissionMap = Record<number, number>; // moduleId -> flags

@Injectable({ providedIn: 'root' })
export class MeService {
  private http = inject(HttpClient);
  private access = inject(AccessService);
  private auth = inject(AuthService);
  private base = `${environment.apiUrl}/me`;

  private _loaded = new BehaviorSubject<boolean>(false);
  loaded = () => this._loaded.value;

  constructor() {
    // When auth state changes, reset loaded and/or clear
    this.auth.stateChanges.subscribe(s => {
      if (!s.isAuthenticated) {
        this.clear();
      } else {
        this._loaded.next(false); // force next ensureLoaded() to fetch
      }
    });
  }

  loadPermissions() {
    if (this._loaded.value || !this.auth.isAuthenticated) return;
    this.http
      .get<PermissionMap>(
        `${this.base}/permissions`,
        { context: new HttpContext().set(QUIET, true).set(API_ENVELOPE, true) }
      )
      .pipe(tap((map) => this.access.setAll(map)))
      .subscribe({
        next: () => this._loaded.next(true),
        error: () => this._loaded.next(true),
      });
  }

  refreshPermissions() {
    if (!this.auth.isAuthenticated) {
      this._loaded.next(false);
      this.access.clear();
      return throwError(() => new Error('Not authenticated'));
    }
    return this.http.get<PermissionMap>(
      `${this.base}/permissions`,
      { context: new HttpContext().set(QUIET, true).set(API_ENVELOPE, true) }
    ).pipe(
      tap((map) => this.access.setAll(map)),
      tap(() => this._loaded.next(true)),
    );
  }

  async ensureLoaded(): Promise<boolean> {
    if (this._loaded.value) return true;
    if (!this.auth.isAuthenticated) return false;
    try {
      await firstValueFrom(this.refreshPermissions());
      return true;
    } catch {
      this._loaded.next(true);
      return false;
    }
  }

  clear() {
    this.access.clear();
    this._loaded.next(false);
  }
}
