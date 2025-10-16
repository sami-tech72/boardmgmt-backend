import { Component, ViewChild, inject, computed, signal, Pipe, PipeTransform } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MeetingsService, MeetingDto, MeetingStatus, TranscriptDto } from './meetings.service';
import { ScheduleMeetingModal } from '../shared/schedule-meeting-modal/schedule-meeting.modal';
import { UserMenuComponent } from '../shared/user-menu/user-menu.component';
import { DataTableDirective } from '../shared/data-table/data-table.directive';

declare const bootstrap: any;

// Helpers used by the template
@Pipe({ name: 'orderBy', standalone: true })
export class OrderByPipe implements PipeTransform {
  transform<T>(value: T[] | null | undefined, key: keyof T): T[] {
    if (!value) return [];
    return [...value].sort((a: any, b: any) => (a?.[key] ?? 0) - (b?.[key] ?? 0));
  }
}

@Pipe({ name: 'initials', standalone: true })
export class InitialsPipe implements PipeTransform {
  transform(v?: string | null): string {
    if (!v) return '•';
    const parts = v.trim().split(/\s+/).slice(0, 2);
    return parts.map(p => p[0]?.toUpperCase() ?? '').join('') || '•';
  }
}

@Pipe({ name: 'ts', standalone: true })
export class TimeSpanPipe implements PipeTransform {
  transform(v: any): string {
    if (typeof v === 'number') {
      const s = Math.max(0, Math.floor(v));
      const h = Math.floor(s / 3600);
      const m = Math.floor((s % 3600) / 60);
      const ss = s % 60;
      return h ? `${h.toString().padStart(2,'0')}:${m.toString().padStart(2,'0')}:${ss.toString().padStart(2,'0')}`
               : `${m.toString().padStart(2,'0')}:${ss.toString().padStart(2,'0')}`;
    }
    if (typeof v === 'string' && /^\d{1,2}:\d{2}:\d{2}$/.test(v)) return v;
    return String(v ?? '');
  }
}

@Component({
  standalone: true,
  selector: 'app-meetings',
  imports: [
    CommonModule,
    FormsModule,
    DatePipe,
    ScheduleMeetingModal,
    OrderByPipe,
    InitialsPipe,
    TimeSpanPipe,
    UserMenuComponent,
    DataTableDirective,
  ],
  templateUrl: './meetings.page.html',
  styleUrls: ['./meetings.page.scss'],
})
export class MeetingsPage {
  private api = inject(MeetingsService);
  private router = inject(Router);

  @ViewChild(ScheduleMeetingModal) createModal!: ScheduleMeetingModal;

  meetings = signal<MeetingDto[]>([]);
  selectedForJoin = signal<MeetingDto | null>(null);
  selectedForDetails = signal<MeetingDto | null>(null);

  transcript = signal<TranscriptDto | null>(null);
  transcriptLoading = signal<boolean>(false);
  ingesting = signal<boolean>(false);

  loading = signal<boolean>(false);
  error = signal<string | null>(null);

  statusFilter = signal<string>(''); // '', 'draft', 'upcoming', 'active', 'completed', 'cancelled'
  typeFilter = signal<string>('');   // '', 'board', 'committee', 'emergency'
  search = signal<string>('');

  statusLabel(m: MeetingDto): 'draft'|'upcoming'|'active'|'completed'|'cancelled'|'unknown' {
    const now = new Date();
    const start = new Date(m.scheduledAt);
    const end = m.endAt ? new Date(m.endAt) : new Date(start.getTime() + 60 * 60 * 1000);

    if (m.status === MeetingStatus.Cancelled) return 'cancelled';
    if (m.status === MeetingStatus.Completed) return 'completed';
    if (m.status === MeetingStatus.Draft) return 'draft';

    if (now < start) return 'upcoming';
    if (now >= start && now < end) return 'active';
    if (now >= end) return 'completed';
    return 'unknown';
  }

  typeIconClass(type?: string | null) {
    switch (type) {
      case 'board': return 'fas fa-users';
      case 'committee': return 'fas fa-chart-line';
      case 'emergency': return 'fas fa-exclamation-triangle';
      default: return 'fas fa-clipboard-list';
    }
  }

  typeColorClass(type?: string | null) {
    switch (type) {
      case 'board': return 'bg-primary';
      case 'committee': return 'bg-success';
      case 'emergency': return 'bg-danger';
      default: return 'bg-info';
    }
  }

  trackById = (_: number, m: MeetingDto) => m.id;

  filtered = computed(() => {
    const list = this.meetings();
    const status = this.statusFilter();
    const type = this.typeFilter();
    const q = this.search().toLowerCase().trim();

    return list.filter((m) => {
      const s = this.statusLabel(m);
      const matchesStatus = !status || s === status;
      const matchesType = !type || m.type?.toLowerCase?.() === type;
      const hay = `${m.title} ${m.location} ${m.description ?? ''}`.toLowerCase();
      const matchesQ = !q || hay.includes(q);
      return matchesStatus && matchesType && matchesQ;
    });
  });

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.error.set(null);

    this.api.getAll().subscribe({
      next: (list) => { this.meetings.set(list); this.loading.set(false); },
      error: (err) => {
        const msg = err?.error?.message || err?.message || 'Failed to load meetings';
        this.error.set(msg);
        this.loading.set(false);
      },
    });
  }

  clearFilters() {
    this.statusFilter.set('');
    this.typeFilter.set('');
    this.search.set('');
  }

  viewCalendar() {
    this.router.navigate(['/meetings', 'calendar']);
  }

  viewMeeting(id: string) {
    this.api.getById(id).subscribe({
      next: (m) => {
        this.selectedForDetails.set(m);
        this.transcript.set(null); // reset; lazy-load on Chat tab
        const el = document.getElementById('meetingDetailsModal');
        if (!el) return;
        const bsModal = new bootstrap.Modal(el, { backdrop: 'static' });
        bsModal.show();
      },
      error: (e) => console.error('Failed to load meeting', id, e),
    });
  }

  // Transcript controls
  ensureTranscriptLoaded(meetingId: string) {
    if (this.transcript() || this.transcriptLoading()) return;
    this.refreshTranscript(meetingId);
  }

  refreshTranscript(meetingId: string) {
    this.transcriptLoading.set(true);
    this.api.getTranscript(meetingId).subscribe({
      next: (t) => { this.transcript.set(t); this.transcriptLoading.set(false); },
      error: () => { this.transcript.set(null); this.transcriptLoading.set(false); },
    });
  }

  ingestNow(meetingId: string) {
    this.ingesting.set(true);
    this.api.ingestTranscript(meetingId).subscribe({
      next: () => { this.ingesting.set(false); this.refreshTranscript(meetingId); },
      error: (e) => { console.error('Ingest failed', e); this.ingesting.set(false); },
    });
  }

  // Join modal + actions
  openJoinModal(m: MeetingDto) {
    const url = this.generateMeetingLink(m);
    if (!url) return;
    this.selectedForJoin.set(m);
    const el = document.getElementById('joinMeetingModal');
    if (!el) return;
    const bsModal = new bootstrap.Modal(el, { backdrop: 'static' });
    bsModal.show();
  }

  closeJoinModal() {
    const el = document.getElementById('joinMeetingModal');
    if (!el) return;
    const bsModal = bootstrap.Modal.getInstance(el) ?? new bootstrap.Modal(el);
    bsModal.hide();
  }

  launchMeeting() {
    const m = this.selectedForJoin();
    if (!m) return;
    const url = this.generateMeetingLink(m);
    if (!url) return;
    window.open(url, '_blank', 'noopener,noreferrer');
    this.closeJoinModal();
  }

  dialIn() {
    const m = this.selectedForJoin();
    if (!m) return;
    this.router.navigate(['/meetings', m.id, 'dial-in']);
    this.closeJoinModal();
  }

  editMeeting(id: string) {
    this.api.getById(id).subscribe({
      next: (m) => this.createModal.openForEdit(m),
      error: (e) => console.error('Failed to load meeting', id, e),
    });
  }

  viewRecording(id: string) { console.log('view recording for', id); }

  hasJoinLink(m: MeetingDto) { return !!this.generateMeetingLink(m); }

  generateMeetingLink(m: MeetingDto) {
    if (m.joinUrl && /^https?:\/\//i.test(m.joinUrl)) return m.joinUrl;  // preferred
    if (m.location && /^https?:\/\//i.test(m.location)) return m.location; // fallback
    return '';
  }

  copyJoinLink(m?: MeetingDto | null) {
    if (!m) return;
    const url = this.generateMeetingLink(m);
    if (!url) return;
    navigator.clipboard?.writeText(url).catch(() => {});
  }

   onLogout(): void {
  // optional: extra cleanup or message
  console.log('User logged out from dashboard');
  // No need to navigate manually — UserMenuComponent already does that.
}
}
