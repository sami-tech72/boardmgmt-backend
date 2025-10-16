import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface AttendancePoint { month: string; meetings: number; confirmedAttendees: number; present?: number; absent?: number; excused?: number; }
export interface VotingTrendPoint { month: string; polls: number; ballots: number; participationRatePct: number; }
export interface DocumentUsagePoint { month: string; documents: number; sizeBytes: number; }
export interface PerformanceMetricsDto { meetingsScheduled: number; meetingsCompleted: number; avgAgendaItemsPerMeeting: number; avgDocsPerMeeting: number; avgAttendeesPerMeeting: number; pollsPerMeeting: number; }
export interface RecentReportDto { id: string; name: string; type: string; generatedBy: string; generatedAt: string; fileUrl?: string; format?: string; periodLabel?: string; }
export interface ReportsDashboardDto { attendance: AttendancePoint[]; voting: VotingTrendPoint[]; documents: DocumentUsagePoint[]; performance: PerformanceMetricsDto; recent: RecentReportDto[]; }
export interface GenerateReportPayload { type: string; period: string; start?: string; end?: string; includeCharts: boolean; includeData: boolean; includeSummary: boolean; includeRecommendations: boolean; format: string; }

@Injectable({ providedIn: 'root' })
export class ReportsService {
    private http = inject(HttpClient);
  private readonly base = environment.apiUrl || '/api';

  getDashboard(months = 6) {
    console.log('http',this.http);
    
    const params = new HttpParams().set('months', months);
    return this.http.get<ReportsDashboardDto>(`${this.base}/reports/dashboard`, { params });
  }

  getRecent(take = 10) {
    const params = new HttpParams().set('take', take);
    return this.http.get<RecentReportDto[]>(`${this.base}/reports/recent`, { params });
  }

  generateReport(payload: GenerateReportPayload) {
    return this.http.post<{ id: string }>(`${this.base}/reports/generate`, payload);
  }
}
