import { Component, ElementRef, EventEmitter, Output, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  VotingService,
  VoteEligibility,
  VoteType,
  CreateVoteRequest,
  MeetingMinimalDto,
  EligibleVoterDto,
  UserSearchDto,
} from '@core/services/voting.service';

@Component({
  standalone: true,
  selector: 'app-create-vote-modal',
  imports: [CommonModule, FormsModule],
  templateUrl: './create-vote.modal.html',
  styleUrls: ['./create-vote.modal.scss'],
})
export class CreateVoteModal  {
  private svc = inject(VotingService);

  @Output() created = new EventEmitter<string>();
  @ViewChild('modalEl', { static: true }) modalEl!: ElementRef<HTMLDivElement>;
  private modalRef: any;

  // expose enums
  VoteType = VoteType;
  VoteEligibility = VoteEligibility;

  // form model
 title: string = '';
  description = '';
  type: VoteType = VoteType.YesNo;
  allowAbstain = true;
  anonymous = false;
  deadlineLocal = this.initDefaultDeadline();
  eligibility: VoteEligibility = VoteEligibility.Public;

  meetingId: string = '';
  agendaItemId: string | null = null;

  options: string[] = ['', ''];

  userSearch = '';
  userResults: UserSearchDto[] = [];
  selectedUsers: Array<{ id: string; display: string }> = [];

  meetings: MeetingMinimalDto[] = [];
  attendees: EligibleVoterDto[] = [];
  loadingAttendees = false;

  ngOnInit() {
    // Load meetings list for picker
    this.svc.listMeetingsMinimal().subscribe((m) => (this.meetings = m ?? []));
  }

  private ensureModal() {
    if (this.modalRef) return;
    const bs = (window as any)?.bootstrap;
    if (bs?.Modal && this.modalEl?.nativeElement) {
      this.modalRef = bs.Modal.getOrCreateInstance(this.modalEl.nativeElement, {
        backdrop: 'static',
        keyboard: false,
      });
    }
  }

  open() {
    this.ensureModal();
    this.resetRuntimeOnly();
    this.modalRef?.show?.();
  }

  close() {
    this.modalRef?.hide?.();
  }

  // UI helpers
  onTypeChange() {
    if (this.type !== VoteType.MultipleChoice) {
      this.options = ['', ''];
    } else if (this.options.length < 2) {
      this.options = ['', ''];
    }
  }

  onEligibilityChange() {
    if (this.eligibility !== VoteEligibility.SpecificUsers) {
      this.selectedUsers = [];
      this.userResults = [];
      this.userSearch = '';
    }
    this.refreshAttendeesIfNeeded();
  }

  onMeetingChange() {
    this.refreshAttendeesIfNeeded();
    if (this.eligibility === VoteEligibility.SpecificUsers && this.meetingId) {
      const allowedIds = new Set((this.attendees ?? []).map((a) => (a.userId || a.id)));
      this.selectedUsers = this.selectedUsers.filter((u) => allowedIds.has(u.id));
    }
  }

  private refreshAttendeesIfNeeded() {
    if (
      (this.eligibility === VoteEligibility.MeetingAttendees ||
        this.eligibility === VoteEligibility.SpecificUsers) &&
      this.meetingId
    ) {
      this.loadAttendees(this.meetingId);
    } else {
      this.attendees = [];
    }
  }

  private loadAttendees(meetingId: string) {
    this.loadingAttendees = true;
    this.attendees = [];
    this.svc.listEligibleVoters(meetingId).subscribe({
      next: (list) => {
        this.attendees = list ?? [];
        this.loadingAttendees = false;
      },
      error: () => {
        this.attendees = [];
        this.loadingAttendees = false;
      },
    });
  }

  addOption() { this.options.push(''); }
  removeOption(index: number) {
    if (this.options.length <= 2) return;
    this.options.splice(index, 1);
  }

  // Specific Users: typeahead + selection
  onSearchUsers() {
    const q = (this.userSearch || '').trim();
    if (q.length < 2) {
      this.userResults = [];
      return;
    }
    this.svc.searchUsers(q).subscribe((res) => {
      const already = new Set(this.selectedUsers.map((s) => s.id));
      this.userResults = (res ?? []).filter((u) => !already.has(u.id));
    });
  }

  addUserFromSearch(u: UserSearchDto) {
    const display = u.displayName || u.userName || u.email || u.id;
    if (!this.selectedUsers.some((x) => x.id === u.id)) {
      this.selectedUsers.push({ id: u.id, display });
    }
    this.userSearch = '';
    this.userResults = [];
  }

  addAllAttendeesToSpecific() {
    if (this.eligibility !== VoteEligibility.SpecificUsers) return;
    const ids = new Set(this.selectedUsers.map((s) => s.id));
    for (const a of this.attendees) {
      const key = a.userId || a.id;
      if (!ids.has(key)) {
        this.selectedUsers.push({ id: key, display: a.name });
        ids.add(key);
      }
    }
  }

  removeSelectedUser(id: string) {
    this.selectedUsers = this.selectedUsers.filter((u) => u.id !== id);
  }

  byId = (_: number, a: EligibleVoterDto) => (a.userId || a.id);

  isAttendeeChecked(a: EligibleVoterDto): boolean {
    if (this.eligibility === VoteEligibility.MeetingAttendees) return true;
    const key = a.userId || a.id;
    for (let i = 0; i < this.selectedUsers.length; i++) {
      if (this.selectedUsers[i].id === key) return true;
    }
    return false;
  }

  onAttendeeToggled(a: EligibleVoterDto, checked: boolean): void {
    if (this.eligibility === VoteEligibility.MeetingAttendees) return;
    const key = a.userId || a.id;
    if (checked) {
      let exists = false;
      for (let i = 0; i < this.selectedUsers.length; i++) {
        if (this.selectedUsers[i].id === key) { exists = true; break; }
      }
      if (!exists) this.selectedUsers.push({ id: key, display: a.name });
    } else {
      this.removeSelectedUser(key);
    }
  }

  // Submit
  get canSubmit(): boolean {
    if (!this.title.trim()) return false;
    if (!this.deadlineLocal) return false;

    if (this.type === VoteType.MultipleChoice) {
      const clean = this.options.map((o) => o.trim()).filter(Boolean);
      if (clean.length < 2) return false;
    }

    if (this.eligibility === VoteEligibility.MeetingAttendees && !this.meetingId) return false;

    if (this.eligibility === VoteEligibility.SpecificUsers && this.selectedUsers.length === 0) {
      return false;
    }

    return true;
  }

  create() {
    if (!this.canSubmit) return;

    const body: CreateVoteRequest = {
      title: this.title.trim(),
      description: this.description?.trim() || null,
      type: this.type,
      allowAbstain: this.allowAbstain,
      anonymous: this.anonymous,
      deadline: this.asIsoUtc(this.deadlineLocal),
      eligibility: this.eligibility,
      meetingId:
        this.eligibility === VoteEligibility.MeetingAttendees || this.meetingId
          ? (this.meetingId || null)
          : null,
      agendaItemId: this.agendaItemId,
      options:
        this.type === VoteType.MultipleChoice
          ? this.options.map((o) => o.trim()).filter(Boolean)
          : null,
      specificUserIds:
        this.eligibility === VoteEligibility.SpecificUsers
          ? this.selectedUsers.map((u) => u.id)
          : null,
    };

    this.svc.createVote(body).subscribe(({ id }) => {
      this.created.emit?.(id);
      this.resetForm();
      this.close();
    });
  }

  // Utils
  private asIsoUtc(dtLocal: string): string {
    return new Date(dtLocal).toISOString();
  }

  private initDefaultDeadline(): string {
    const d = new Date(Date.now() + 72 * 3600 * 1000);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  minDeadline = (() => {
    const pad = (n: number) => String(n).padStart(2, '0');
    const d = new Date();
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  })();

  private resetRuntimeOnly() {
    this.userSearch = '';
    this.userResults = [];
  }

  resetForm() {
    this.title = '';
    this.description = '';
    this.type = VoteType.YesNo;
    this.allowAbstain = true;
    this.anonymous = false;
    this.deadlineLocal = this.initDefaultDeadline();
    this.eligibility = VoteEligibility.Public;
    this.meetingId = '';
    this.agendaItemId = null;
    this.options = ['', ''];
    this.selectedUsers = [];
    this.userResults = [];
    this.attendees = [];
    this.loadingAttendees = false;
  }
  onEligibilityModelChange(val: any) {
  this.eligibility = Number(val) as VoteEligibility; // keep enum numeric
  this.onEligibilityChange();                        // run your existing logic
}

}
