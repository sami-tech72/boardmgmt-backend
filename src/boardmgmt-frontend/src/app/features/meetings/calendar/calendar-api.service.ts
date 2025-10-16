// src/app/features/meetings/calendar/calendar-api.service.ts
import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { Observable } from 'rxjs';

export interface CalendarEventDto {
  id: string;
  subject: string;
  startUtc: string;  // ISO
  endUtc: string;    // ISO
  joinUrl?: string | null;
  provider?: 'Zoom' | 'Microsoft365' | null;
}

type Provider = 'Zoom' | 'Microsoft365' | 'All'; // used for /range if you also query providers

@Injectable({ providedIn: 'root' })
export class CalendarApiService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/calendar`;

  // ✅ Pure DB-backed range
  getRangeFromDb(start: Date, end: Date): Observable<CalendarEventDto[]> {
    const params = new HttpParams()
      .set('start', start.toISOString())
      .set('end', end.toISOString());
    return this.http.get<CalendarEventDto[]>(`${this.base}/range-db`, { params });
  }

  // (optional) If you still want to merge provider APIs:
  getRange(start: Date, end: Date, provider: Provider): Observable<CalendarEventDto[]> {
    const params = new HttpParams()
      .set('start', start.toISOString())
      .set('end', end.toISOString())
      .set('provider', provider);
    return this.http.get<CalendarEventDto[]>(`${this.base}/range`, { params });
  }

  // src/app/features/meetings/calendar/calendar-api.service.ts
moveEvent(id: string, start: Date, end?: Date) {
  const url = `${this.base}/move/${id}`;
  const body = {
    startUtc: start.toISOString(),
    endUtc: end ? end.toISOString() : null
  };
  return this.http.put<void>(url, body);
}
delete(id: string) {
  return this.http.delete<void>(`${this.base.replace('/calendar','')}/meetings/${id}`);
}
}
