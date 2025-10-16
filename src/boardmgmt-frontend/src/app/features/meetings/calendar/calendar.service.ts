// app/calendar/calendar.service.ts
import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { Observable } from 'rxjs';

export interface CalendarEventDto {
  id: string;
  subject: string;
  startUtc: string;
  endUtc: string;
  joinUrl?: string | null;
  provider?: 'Zoom' | 'Microsoft365' | null;
}

@Injectable({ providedIn: 'root' })
export class CalendarService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/calendar`;

  listRange(
    startIso: string,
    endIso: string,
    provider: 'Zoom' | 'Microsoft365' | 'All' = 'All',
  ): Observable<CalendarEventDto[]> {
    const params = new URLSearchParams({ start: startIso, end: endIso, provider });
    return this.http.get<CalendarEventDto[]>(`${this.base}/range?${params.toString()}`);
  }
}
