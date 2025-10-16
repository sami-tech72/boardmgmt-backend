import {
  Component,
  AfterViewInit,
  OnDestroy,
  ViewChild,
  ElementRef,
  inject,
  signal,
  ChangeDetectorRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Chart } from 'chart.js/auto';

import {
  ReportsService,
  ReportsDashboardDto,
  GenerateReportPayload,
} from './reports.service';
import { GenerateReportModal } from '../shared/generate-report-modal/generate-report.modal';
import { BROWSER_STORAGE } from '@core/tokens/browser-storage.token';
import { UserMenuComponent } from '../shared/user-menu/user-menu.component';

declare const bootstrap: any;

@Component({
  standalone: true,
  selector: 'app-reports',
  imports: [CommonModule, GenerateReportModal,UserMenuComponent],
  templateUrl: './reports.page.html',
  styleUrls: ['./reports.page.scss'],
})
export class ReportsPage implements AfterViewInit, OnDestroy {
  private svc = inject(ReportsService);
  private cdr = inject(ChangeDetectorRef);
  private storage = inject(BROWSER_STORAGE);

  data = signal<ReportsDashboardDto | null>(null);
  loadError = signal<string | null>(null);

  private attendanceEl?: HTMLCanvasElement;
  private votingEl?: HTMLCanvasElement;
  private attendanceChart?: Chart;
  private votingChart?: Chart;

  @ViewChild('attendanceCanvas')
  set attendanceCanvasRef(ref: ElementRef<HTMLCanvasElement> | undefined) {
    this.attendanceEl = ref?.nativeElement;
    this.tryRenderCharts();
  }

  @ViewChild('votingCanvas')
  set votingCanvasRef(ref: ElementRef<HTMLCanvasElement> | undefined) {
    this.votingEl = ref?.nativeElement;
    this.tryRenderCharts();
  }

  ngAfterViewInit() {
    this.waitForTokenThenLoad();
  }

  ngOnDestroy() {
    this.attendanceChart?.destroy();
    this.votingChart?.destroy();
  }

  // ---------- UI actions ----------
  exportData() {
    const d = this.data();
    if (!d) return;
    const rows: string[] = ['Section,Month,Metric,Value'];

    d.attendance.forEach((a) => {
      rows.push(`Attendance,${a.month},Meetings,${a.meetings}`);
      rows.push(`Attendance,${a.month},ConfirmedAttendees,${a.confirmedAttendees}`);
      if (typeof a.present === 'number')  rows.push(`Attendance,${a.month},Present,${a.present}`);
      if (typeof a.absent === 'number')   rows.push(`Attendance,${a.month},Absent,${a.absent}`);
      if (typeof a.excused === 'number')  rows.push(`Attendance,${a.month},Excused,${a.excused}`);
    });

    d.voting.forEach((v) => {
      rows.push(`Voting,${v.month},Polls,${v.polls}`);
      rows.push(`Voting,${v.month},Ballots,${v.ballots}`);
      rows.push(`Voting,${v.month},ParticipationRatePct,${v.participationRatePct}`);
    });

    d.documents.forEach((doc) => {
      rows.push(`Documents,${doc.month},Documents,${doc.documents}`);
      rows.push(`Documents,${doc.month},SizeBytes,${doc.sizeBytes}`);
    });

    const blob = new Blob([rows.join('\n')], { type: 'text/csv' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'reports_dashboard.csv';
    a.click();
    URL.revokeObjectURL(a.href);
  }

  showReports(category: string) {
    console.log('Filter by category:', category);
  }

  viewReport(id: string) {
    const rec = this.data()?.recent.find((x) => x.id === id);
    if (rec?.fileUrl) window.open(rec.fileUrl, '_blank');
  }

  downloadReport(id: string) { this.viewReport(id); }

  shareReport(id: string) {
    const rec = this.data()?.recent.find((x) => x.id === id);
    if (!rec?.fileUrl) return;
    navigator.clipboard.writeText(rec.fileUrl);
    alert('Report link copied to clipboard');
  }

  onGenerate(payload: GenerateReportPayload) {
    this.svc.generateReport(payload).subscribe({
      next: (res) => {
        if (!res?.id) return;
        this.load(); // refresh dashboard + recent
        const el = document.getElementById('reportGeneratorModal');
        if (el) bootstrap.Modal.getOrCreateInstance(el).hide();
      },
      error: () => alert('Failed to generate report'),
    });
  }

  // ---------- Data loading ----------
  private waitForTokenThenLoad(retries = 40) {
  const token = this.storage.getItem('jwt');
  if (token) { this.load(); return; }
  if (retries <= 0) { 
    console.warn('No JWT found; skipping load to avoid 401');
    return; 
  }
  setTimeout(() => this.waitForTokenThenLoad(retries - 1), 150);
}


  private load() {
    this.loadError.set(null);
    this.svc.getDashboard(6).subscribe({
      next: (res) => {
        // res is ReportsDashboardDto (unwrapped by your interceptor)
        console.log('Dashboard response', res);
        if (!res) { this.loadError.set('No data returned'); return; }
        this.data.set({
          attendance: res.attendance ?? [],
          voting: res.voting ?? [],
          documents: res.documents ?? [],
          performance: res.performance ?? {
            meetingsScheduled: 0,
            meetingsCompleted: 0,
            avgAgendaItemsPerMeeting: 0,
            avgDocsPerMeeting: 0,
            avgAttendeesPerMeeting: 0,
            pollsPerMeeting: 0,
          },
          recent: res.recent ?? [],
        });
        queueMicrotask(() => {
          this.cdr.detectChanges();
          this.tryRenderCharts();
        });
      },
      error: (err) => {
        console.error('Dashboard load error', err);
        this.loadError.set(err?.status === 401 ? 'Unauthorized' : 'Failed to load dashboard');
      },
    });
  }

  // ---------- Charts (donut + soft-filled line) ----------
  private tryRenderCharts() {
  const d = this.data();
  if (!d || !this.attendanceEl || !this.votingEl) return;

  requestAnimationFrame(() => {
    setTimeout(() => {
      const monthLabel = (ym: string) => {
        const [y, m] = ym.split('-').map(Number);
        return new Date(y, (m || 1) - 1, 1).toLocaleString(undefined, { month: 'short' });
      };
      const sum = (arr: number[]) => arr.reduce((a, b) => a + (b || 0), 0);

      // ---- Attendance totals ----
      const totalsPresent = sum(d.attendance.map(a => a.present ?? a.confirmedAttendees ?? 0));
      const totalsAbsent  = sum(d.attendance.map(a => a.absent ?? 0));
      const totalsExcused = sum(d.attendance.map(a => a.excused ?? 0));
      const totalSum = totalsPresent + totalsAbsent + totalsExcused;

      // Placeholder if total == 0
      const donutData   = totalSum > 0 ? [totalsPresent, totalsAbsent, totalsExcused] : [1];
      const donutColors = totalSum > 0 ? ['#2ecc71', '#e74c3c', '#f1c40f'] : ['#bdc3c7'];
      const donutLabels = totalSum > 0 ? ['Present', 'Absent', 'Excused'] : ['No Data'];

      // Center text plugin (draw total or “No data”)
      const centerTextPlugin = {
        id: 'centerText',
        afterDraw: (chart: any) => {
          const { ctx, chartArea } = chart;
          if (!chartArea) return;
          const text = totalSum > 0 ? `${totalSum}` : 'No data';
          ctx.save();
          ctx.font = '600 16px system-ui, -apple-system, "Segoe UI", Roboto, Arial';
          ctx.fillStyle = '#333';
          ctx.textAlign = 'center';
          ctx.textBaseline = 'middle';
          ctx.fillText(text, (chartArea.left + chartArea.right) / 2, (chartArea.top + chartArea.bottom) / 2);
          ctx.restore();
        }
      };

      // ---- Attendance donut ----
      this.attendanceChart?.destroy();
      const actx = this.attendanceEl!.getContext('2d')!;
      this.attendanceChart = new Chart(actx, {
        type: 'doughnut',
        data: {
          labels: donutLabels,
          datasets: [{ data: donutData, backgroundColor: donutColors, borderWidth: 0, hoverOffset: 6 }],
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          cutout: '65%',
          plugins: {
            legend: {
              display: totalSum > 0, // hide if placeholder
              position: 'bottom',
              labels: { usePointStyle: true, boxWidth: 10 },
            },
            tooltip: {
              enabled: totalSum > 0,
              callbacks: {
                label: (ctx) => {
                  const val = ctx.parsed ?? 0;
                  const pct = totalSum ? Math.round((val / totalSum) * 100) : 0;
                  return `${ctx.label}: ${val} (${pct}%)`;
                },
              },
            },
          },
        },
        plugins: [centerTextPlugin],
      });

      // ---- Voting line (soft fill) ----
      const monthsV = d.voting.map(v => monthLabel(v.month));
      const participation = d.voting.map(v => v.participationRatePct ?? 0);
      const vctx = this.votingEl!.getContext('2d')!;
      // if CSS controls size, canvas height prop may be 0; use clientHeight fallback for gradient
      const h = this.votingEl!.height || this.votingEl!.clientHeight || 320;
      const gradient = vctx.createLinearGradient(0, 0, 0, h);
      gradient.addColorStop(0, 'rgba(52, 152, 219, 0.35)');
      gradient.addColorStop(1, 'rgba(52, 152, 219, 0.05)');

      this.votingChart?.destroy();
      this.votingChart = new Chart(vctx, {
        type: 'line',
        data: {
          labels: monthsV,
          datasets: [{
            label: 'Participation',
            data: participation,
            fill: true,
            backgroundColor: gradient,
            borderColor: '#2980b9',
            borderWidth: 2,
            pointRadius: 3,
            pointHoverRadius: 5,
            tension: 0.35,
          }],
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          scales: {
            y: { min: 0, max: 100, ticks: { callback: (v) => `${v}%`, stepSize: 20 }, grid: { color: 'rgba(0,0,0,0.05)' } },
            x: { grid: { display: false } },
          },
          plugins: {
            legend: { display: false },
            tooltip: { callbacks: { label: (ctx) => ` ${ctx.parsed.y ?? 0}%` } },
          },
          elements: { line: { borderCapStyle: 'round' }, point: { hitRadius: 10 } },
        },
      });
    }, 0);
  });
}
onLogout(): void {
  // optional: extra cleanup or message
  console.log('User logged out from dashboard');
  // No need to navigate manually — UserMenuComponent already does that.
}
}
