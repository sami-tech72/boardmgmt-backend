import {
  Component,
  inject,
  OnInit,
  signal,
  ViewChild,
  ElementRef,
  ChangeDetectorRef,
} from '@angular/core';
import { CommonModule, NgClass, NgFor, NgIf } from '@angular/common';
import { forkJoin } from 'rxjs';
import { DashboardService } from './dashboard.service';
import {
  DashboardStats,
  DashboardMeeting,
  DashboardDocument,
  DashboardActivity,
} from './dashboard.models';
import { ScheduleMeetingModal } from '../shared/schedule-meeting-modal/schedule-meeting.modal';
import { UploadDocumentModal } from '../shared/upload-document-modal/upload-document.modal';
import { CreateVoteModal } from '../shared/create-vote-modal/create-vote.modal';
import { StatsDetailModalComponent } from '../shared/stats-detail-modal/stats-detail-modal.component';
import { StatsKind } from './dashboard.api';
import { UserMenuComponent } from '../shared/user-menu/user-menu.component';
import { PageHeaderComponent } from '../shared/page-header/page-header.component';


// Meetings feature to fetch full meeting & transcripts
import {
  MeetingsService,
  MeetingDto,
  TranscriptDto,
} from '../meetings/meetings.service';
import { Router } from '@angular/router';

@Component({
  standalone: true,
  selector: 'app-dashboard',
  imports: [
    CommonModule,
    NgIf,
    NgFor,
    NgClass,
    ScheduleMeetingModal,
    UploadDocumentModal,
    CreateVoteModal,
    StatsDetailModalComponent,
    UserMenuComponent,
    PageHeaderComponent,
  ],
  templateUrl: './dashboard.page.html',
  styleUrls: ['./dashboard.page.scss'],
})
export class DashboardPage implements OnInit {

   private router = inject(Router); 
  // ---------- existing dashboard state ----------
  stats = signal<DashboardStats | null>(null);
  meetings = signal<DashboardMeeting[]>([]);
  documents = signal<DashboardDocument[]>([]);
  activity = signal<DashboardActivity[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  private api = inject(DashboardService);
  private meetingsApi = inject(MeetingsService);
  private cdr = inject(ChangeDetectorRef);

  // ---------- modals already present ----------
  @ViewChild('createModal') createModal?: ScheduleMeetingModal;
  @ViewChild(UploadDocumentModal) uploadModal!: UploadDocumentModal;
  @ViewChild('voteModal') voteModal!: CreateVoteModal;
  @ViewChild(StatsDetailModalComponent) statsModal!: StatsDetailModalComponent;

  // ---------- meeting modals state ----------
  selectedForJoin = signal<MeetingDto | null>(null);
  selectedForDetails = signal<MeetingDto | null>(null);

  transcript = signal<TranscriptDto | null>(null);
  transcriptLoading = signal<boolean>(false);
  ingesting = signal<boolean>(false);

  // ---------- Modal element refs ----------
  @ViewChild('joinModalEl') joinModalEl!: ElementRef<HTMLDivElement>;
  @ViewChild('detailsModalEl') detailsModalEl!: ElementRef<HTMLDivElement>;

  // cache flag so we only load bootstrap bundle once
  private bootstrapReady = false;

  ngOnInit(): void {
    this.refresh();
  }

  // ========== SSR-safe Bootstrap loader ==========
  /** Ensures bootstrap.bundle is loaded in the BROWSER only. */
  private async ensureBootstrapBundle(): Promise<void> {
    if (this.bootstrapReady) return;
    if (typeof window === 'undefined') return; // SSR: do nothing
    // Load the bundle (Modal plugin included). Cached after first time.
    await import('bootstrap/dist/js/bootstrap.bundle.min.js');
    this.bootstrapReady = true;
  }

  /** Returns a Modal constructor by dynamically importing 'bootstrap' in the browser. */
  private async getModalCtor(): Promise<any /* Modal ctor */> {
    if (typeof window === 'undefined') return null; // SSR guard
    const mod = await import('bootstrap');
    return mod.Modal;
  }

  // ---------- existing helpers ----------
  openUpload() { this.uploadModal.openForCreate(); }
  openStats(kind: StatsKind) { this.statsModal.open(kind); }
  openVoteModal() { this.voteModal?.open(); }
  load() { this.refresh(); }
  goTo(url: string) {
    this.router.navigateByUrl(url.startsWith('/') ? url : `/${url}`);
  }

  refresh(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      stats: this.api.getStats(),
      meetings: this.api.getRecentMeetings(3),
      documents: this.api.getRecentDocuments(3),
      activity: this.api.getRecentActivity(10),
    }).subscribe({
      next: ({ stats, meetings, documents, activity }) => {
        this.stats.set(stats);
        this.meetings.set(meetings);
        this.documents.set(documents);
        this.activity.set(activity);
      },
      error: () => this.error.set('Failed to load dashboard'),
      complete: () => this.loading.set(false),
    });
  }

  openScheduleModal() { this.createModal?.open(); }

  docIconClass(kind: string) {
    switch (kind) {
      case 'pdf': return 'fa-file-pdf';
      case 'word': return 'fa-file-word';
      case 'excel': return 'fa-file-excel';
      case 'ppt': return 'fa-file-powerpoint';
      default: return 'fa-file-alt';
    }
  }

  timelineMarkerClass(color?: string) {
    return { 'timeline-marker': true, [`bg-${color ?? 'primary'}`]: true };
  }

  // =====================================================
  // Reuse meeting DETAILS + JOIN modals on Dashboard
  // =====================================================

  /** Open details modal by meeting id (fetches full record). */
  viewMeeting(id: string) {
    this.meetingsApi.getById(id).subscribe({
      next: async (m) => {
        this.selectedForDetails.set(m);
        this.transcript.set(null); // lazy load on Chat tab

        // Ensure DOM for *ngIf is rendered
        this.cdr.detectChanges();

        // Load bootstrap JS on the client and open the modal
        await this.ensureBootstrapBundle();
        const Modal = await this.getModalCtor();
        if (!Modal) return; // SSR safeguard
        const modal = Modal.getOrCreateInstance(this.detailsModalEl.nativeElement, { backdrop: 'static' });
        modal.show();
      },
      error: (e) => console.error('Failed to load meeting for details', id, e),
    });
  }

  /** Convenience from Recent Meetings list: fetch then open join modal. */
  openJoinById(id: string) {
    this.meetingsApi.getById(id).subscribe({
      next: (m) => void this.openJoinModal(m),
      error: (e) => console.error('Failed to load meeting for join', id, e),
    });
  }

  /** Open join modal for a full MeetingDto. */
  async openJoinModal(m: MeetingDto) {
    const url = this.generateMeetingLink(m);
    if (!url) return; // no link -> silently ignore or show toast
    this.selectedForJoin.set(m);

    // Ensure DOM for *ngIf is rendered
    this.cdr.detectChanges();

    // Load bootstrap JS on the client and open the modal
    await this.ensureBootstrapBundle();
    const Modal = await this.getModalCtor();
    if (!Modal) return; // SSR safeguard
    const modal = Modal.getOrCreateInstance(this.joinModalEl.nativeElement, { backdrop: 'static' });
    modal.show();
  }

  async closeJoinModal() {
    await this.ensureBootstrapBundle();
    const Modal = await this.getModalCtor();
    if (!Modal) return;
    const modal = Modal.getOrCreateInstance(this.joinModalEl.nativeElement);
    modal.hide();
  }

  launchMeeting() {
    const m = this.selectedForJoin();
    if (!m) return;
    const url = this.generateMeetingLink(m);
    if (!url) return;
    window.open(url, '_blank', 'noopener,noreferrer');
    void this.closeJoinModal();
  }

  dialIn() {
    const m = this.selectedForJoin();
    if (!m) return;
    // this.router.navigate(['/meetings', m.id, 'dial-in']);
    void this.closeJoinModal();
  }

  copyJoinLink(m?: MeetingDto | null) {
    if (!m) return;
    const url = this.generateMeetingLink(m);
    if (!url) return;
    navigator.clipboard?.writeText(url).catch(() => {});
  }

  hasJoinLink(m: MeetingDto) { return !!this.generateMeetingLink(m); }

  generateMeetingLink(m: MeetingDto) {
    if (m.joinUrl && /^https?:\/\//i.test(m.joinUrl)) return m.joinUrl;  // preferred
    if (m.location && /^https?:\/\//i.test(m.location)) return m.location; // fallback
    return '';
  }

  // ---------- Transcript controls ----------
  ensureTranscriptLoaded(meetingId: string) {
    if (this.transcript() || this.transcriptLoading()) return;
    this.refreshTranscript(meetingId);
  }

  refreshTranscript(meetingId: string) {
    this.transcriptLoading.set(true);
    this.meetingsApi.getTranscript(meetingId).subscribe({
      next: (t) => { this.transcript.set(t); this.transcriptLoading.set(false); },
      error: () => { this.transcript.set(null); this.transcriptLoading.set(false); },
    });
  }

  ingestNow(meetingId: string) {
    this.ingesting.set(true);
    this.meetingsApi.ingestTranscript(meetingId).subscribe({
      next: () => { this.ingesting.set(false); this.refreshTranscript(meetingId); },
      error: (e) => { console.error('Ingest failed', e); this.ingesting.set(false); },
    });
  }

  // ---------- Tiny inline helpers used by the transcript tab ----------
  initials(v?: string | null): string {
    if (!v) return '•';
    const parts = v.trim().split(/\s+/).slice(0, 2);
    const s = parts.map(p => p[0]?.toUpperCase() ?? '').join('');
    return s || '•';
  }

  ts(v: any): string {
    if (typeof v === 'number') {
      const s = Math.max(0, Math.floor(v));
      const h = Math.floor(s / 3600);
      const m = Math.floor((s % 3600) / 60);
      const ss = s % 60;
      return h
        ? `${h.toString().padStart(2,'0')}:${m.toString().padStart(2,'0')}:${ss.toString().padStart(2,'0')}`
        : `${m.toString().padStart(2,'0')}:${ss.toString().padStart(2,'0')}`;
    }
    if (typeof v === 'string' && /^\d{1,2}:\d{2}:\d{2}$/.test(v)) return v;
    return String(v ?? '');
  }

  // ---------- Placeholder for Minutes button ----------
  openMinutes(meetingId: string) {
    console.log('open minutes for', meetingId);
  }
  onLogout(): void {
  // optional: extra cleanup or message
  console.log('User logged out from dashboard');
  // No need to navigate manually — UserMenuComponent already does that.
}

}
