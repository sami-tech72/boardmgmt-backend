import { HttpClient, HttpParams, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, catchError, of, throwError, OperatorFunction } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENVELOPE, QUIET } from '@core/interceptors/api-envelope.interceptor';

export interface UserDto {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
  isActive?: boolean;
  departmentId?: string | null;
  departmentName?: string | null;
}

export interface MinimalUser {
  id: string;
  name: string;
  email?: string;
  roles?: string[];
  isActive?: boolean;
  departmentId?: string | null;
  departmentName?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
}
export interface RoleDto {
  id: string;
  name: string;
  permissions?: Record<string, number>;
}
export interface RoleOption {
  id: string;
  name: string;
}
export interface DepartmentDto {
  id: string;
  name: string;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class UsersService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/`;

  private onForbiddenReturn<T>(fallback: () => T, suppress: boolean): OperatorFunction<T, T> {
    return catchError((err: HttpErrorResponse) => {
      if (suppress && err.status === 403) {
        return of(fallback());
      }
      return throwError(() => err);
    });
  }

  private unwrapArray<T>(body: any): T[] {
    if (Array.isArray(body)) return body as T[];
    if (Array.isArray(body?.data)) return body.data as T[];
    if (Array.isArray(body?.items)) return body.items as T[];
    return [];
  }
  private unwrapPaged<T>(body: any): PagedResult<T> {
    if (Array.isArray(body)) return { items: body as T[], total: (body as T[]).length };
    const items = this.unwrapArray<T>(body);
    const total = typeof body?.total === 'number' ? body.total : items.length;
    return { items, total };
  }

  getUsers(
    input: {
      q?: string;
      page?: number;
      pageSize?: number;
      activeOnly?: boolean;
      roleNames?: string[];
      departmentId?: string | null;
    } = {},
    opts: { suppressForbidden?: boolean } = {},
  ): Observable<PagedResult<UserDto>> {
    let params = new HttpParams();
    if (input.q) params = params.set('q', input.q);
    if (input.page) params = params.set('page', String(input.page));
    if (input.pageSize) params = params.set('pageSize', String(input.pageSize));
    if (typeof input.activeOnly === 'boolean')
      params = params.set('activeOnly', String(input.activeOnly));
    if (input.roleNames?.length) params = params.set('roles', input.roleNames.join(','));
    if (input.departmentId) params = params.set('departmentId', input.departmentId);

    let context = new HttpContext().set(API_ENVELOPE, true);
    if (opts.suppressForbidden) {
      context = context.set(QUIET, true);
    }

    return this.http
      .get<any>(`${this.base}auth`, { params, context })
      .pipe(
        map((body) => this.unwrapPaged<UserDto>(body)),
        this.onForbiddenReturn(() => ({ items: [], total: 0 }), opts.suppressForbidden ?? false),
      );
  }

  getAllUsersFlat(opts: { suppressForbidden?: boolean } = {}): Observable<UserDto[]> {
    let context = new HttpContext().set(API_ENVELOPE, true);
    if (opts.suppressForbidden) {
      context = context.set(QUIET, true);
    }

    return this.http
      .get<any>(`${this.base}auth`, { context })
      .pipe(
        map((body) => this.unwrapArray<UserDto>(body)),
        this.onForbiddenReturn(() => [], opts.suppressForbidden ?? false),
      );
  }

  getRoles(opts: { suppressForbidden?: boolean } = {}): Observable<RoleOption[]> {
    let context = new HttpContext().set(API_ENVELOPE, true);
    if (opts.suppressForbidden) {
      context = context.set(QUIET, true);
    }

    return this.http
      .get<any>(`${this.base}roles`, { context })
      .pipe(
        map((body) => this.unwrapArray<RoleDto>(body)),
        map((list: RoleDto[]) =>
          list.map((r: RoleDto): RoleOption => ({ id: r.id, name: r.name })),
        ),
        this.onForbiddenReturn(() => [], opts.suppressForbidden ?? false),
      );
  }

  getDepartments(
    q?: string,
    activeOnly?: boolean,
    opts: { suppressForbidden?: boolean } = {},
  ): Observable<DepartmentDto[]> {
    let params = new HttpParams();
    if (q) params = params.set('q', q);
    if (typeof activeOnly === 'boolean') params = params.set('activeOnly', String(activeOnly));
    let context = new HttpContext().set(API_ENVELOPE, true);
    if (opts.suppressForbidden) {
      context = context.set(QUIET, true);
    }
    return this.http
      .get<any>(`${this.base}departments`, {
        params,
        context,
      })
      .pipe(
        map((body) => this.unwrapArray<DepartmentDto>(body)),
        this.onForbiddenReturn(() => [], opts.suppressForbidden ?? false),
      );
  }

  register(input: {
    firstName: string;
    lastName: string;
    email: string;
    password: string;
    role?: string | null;
    departmentId?: string | null;
  }): Observable<{ userId: string; email: string }> {
    return this.http.post<{ userId: string; email: string }>(`${this.base}auth/register`, input, {
      context: new HttpContext().set(API_ENVELOPE, true),
    });
  }

  updateUser(
    userId: string,
    input: {
      firstName?: string | null;
      lastName?: string | null;
      email?: string | null;
      newPassword?: string | null;
      role?: string | null;
      departmentId?: string | null;
      isActive?: boolean | null;
    },
  ) {
    return this.http.put<{ userId: string }>(
      `${this.base}auth/${encodeURIComponent(userId)}`,
      { userId, ...input },
      { context: new HttpContext().set(API_ENVELOPE, true) },
    );
  }

  assignRoles(userId: string, roleNames: string[]) {
    return this.http.put<{ userId: string; roles: string[] }>(
      `${this.base}auth/${encodeURIComponent(userId)}/roles`,
      { roles: roleNames },
      { context: new HttpContext().set(API_ENVELOPE, true) },
    );
  }

  search(query: string) {
    return this.http.get<Array<{ id: string; name: string; email?: string }>>(
      `${this.base}auth/search?query=${encodeURIComponent(query)}`,
    );
  }
}
