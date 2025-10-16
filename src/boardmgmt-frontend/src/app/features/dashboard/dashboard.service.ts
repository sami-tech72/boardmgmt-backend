// src/app/features/dashboard/dashboard.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Observable, catchError, map, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENVELOPE } from '@core/interceptors/api-envelope.interceptor';
import { MOCK_ACTIVITY, MOCK_DOCUMENTS, MOCK_MEETINGS, MOCK_STATS } from './dashboard.mocks';
import {
  StatsKind,
  PagedResult,
  MeetingItem,
  DocumentItem,
  VoteItem,
   ActiveUserItem,   
} from './dashboard.api';

import {
  DashboardStats,
  DashboardMeeting,
  DashboardDocument,
  DashboardActivity,
} from './dashboard.models';

type RawMeeting = {
  id: string;
  title: string;
  subtitle?: string | null;
  startsAtUtc: string; // ISO from API
  status: string; // e.g., "Scheduled" | "Completed" | "Cancelled"
};

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private http = inject(HttpClient);

  // Build a base that always contains /api exactly once.
  private readonly apiRoot = (() => {
    const base = (environment.apiUrl ?? 'http://localhost:5256').replace(/\/+$/, '');
    return base.endsWith('/api') ? base : `${base}/api`;
  })();

  private readonly base = `${this.apiRoot}/dashboard`;

  /** GET /api/dashboard/stats -> DashboardStats */
  getStats(): Observable<DashboardStats> {
    if (environment.useMocks) return of(MOCK_STATS);

    return this.http
      .get<DashboardStats>(`${this.base}/stats`, {
        context: new HttpContext().set(API_ENVELOPE, true),
      })
      .pipe(catchError(() => of(MOCK_STATS)));
  }

  /** GET /api/dashboard/meetings?take=n -> DashboardMeeting[] (normalized) */
  getRecentMeetings(take = 3): Observable<DashboardMeeting[]> {
    const fallback = MOCK_MEETINGS.slice(0, take);
    if (environment.useMocks) return of(fallback);

    return this.http
      .get<RawMeeting[]>(`${this.base}/meetings`, {
        params: new HttpParams().set('take', take),
        context: new HttpContext().set(API_ENVELOPE, true),
      })
      .pipe(
        map((list) => list.map(this.normalizeMeeting)),
        catchError(() => of(fallback)),
      );
  }

  /** GET /api/dashboard/documents?take=n -> DashboardDocument[] */
  getRecentDocuments(take = 3): Observable<DashboardDocument[]> {
    const fallback = MOCK_DOCUMENTS.slice(0, take);
    if (environment.useMocks) return of(fallback);

    return this.http
      .get<DashboardDocument[]>(`${this.base}/documents`, {
        params: new HttpParams().set('take', take),
        context: new HttpContext().set(API_ENVELOPE, true),
      })
      .pipe(catchError(() => of(fallback)));
  }

  /** GET /api/dashboard/activity?take=n -> DashboardActivity[] */
  getRecentActivity(take = 10): Observable<DashboardActivity[]> {
    const fallback = MOCK_ACTIVITY.slice(0, take);
    if (environment.useMocks) return of(fallback);

    return this.http
      .get<DashboardActivity[]>(`${this.base}/activity`, {
        params: new HttpParams().set('take', take),
        context: new HttpContext().set(API_ENVELOPE, true),
      })
      .pipe(catchError(() => of(fallback)));
  }

  // ---- helpers ----

  private normalizeMeeting = (m: RawMeeting): DashboardMeeting => ({
    id: m.id,
    title: m.title,
    subtitle: m.subtitle ?? null,
    // Keep ISO from server; if you want local time ISO, swap to this.toLocalIso(m.startsAtUtc)
    startsAt: m.startsAtUtc,
    status: this.normalizeStatus(m.status),
  });

  private normalizeStatus(apiStatus: string): DashboardMeeting['status'] {
    switch (apiStatus) {
      case 'Scheduled':
      case 'Upcoming':
        return 'Upcoming';
    }
    if (apiStatus === 'Completed') return 'Completed';
    if (apiStatus === 'Cancelled') return 'Cancelled';
    // Sensible default
    return 'Upcoming';
  }

  // If you want local-time ISO strings instead of UTC, use this:
  private toLocalIso(utcIso: string): string {
    const d = new Date(utcIso);
    // produce an ISO-like string without the trailing 'Z' (local time)
    const pad = (n: number) => String(n).padStart(2, '0');
    const yyyy = d.getFullYear();
    const mm = pad(d.getMonth() + 1);
    const dd = pad(d.getDate());
    const hh = pad(d.getHours());
    const mi = pad(d.getMinutes());
    const ss = pad(d.getSeconds());
    const ms = String(d.getMilliseconds()).padStart(3, '0');
    return `${yyyy}-${mm}-${dd}T${hh}:${mi}:${ss}.${ms}`;
  }

  /** GET /api/dashboard/stats/detail?kind=&page=&pageSize= -> PagedResult<...> */
  getStatsDetail(
    kind: StatsKind,
    page = 1,
    pageSize = 10,
  ): Observable<PagedResult<MeetingItem | DocumentItem | VoteItem | ActiveUserItem>> {
    // If you use mocks, you can branch here similarly to others.
    const url = `${this.base}/stats/detail`;
    const params = new HttpParams().set('kind', kind).set('page', page).set('pageSize', pageSize);

    return this.http
      .get<PagedResult<any>>(url, {
        params,
        context: new HttpContext().set(API_ENVELOPE, true),
      })
      .pipe(
        // Minimal normalization for meeting status naming just in case:
        map((res) =>
          kind === 'meetings'
            ? {
                ...res,
                items: (res.items as any[]).map((m) => ({
                  ...m,
                  status: this.normalizeStatus(m.status),
                })),
              }
            : res,
        ),
      );
  }
}
