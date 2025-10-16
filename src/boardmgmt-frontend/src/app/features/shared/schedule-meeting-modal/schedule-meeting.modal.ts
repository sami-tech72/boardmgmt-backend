import { Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  MeetingsService,
  CreateMeetingDto,
  UpdateMeetingDto,
  MeetingDto,
  UpdateAttendeeDto,
  AttendeeDto,
} from '../../meetings/meetings.service';
import { UsersService, MinimalUser } from '../../users/users.service';

declare const bootstrap: any;
type Mode = 'create' | 'edit';

@Component({
  standalone: true,
  selector: 'app-schedule-meeting-modal',
  imports: [CommonModule, FormsModule],
  templateUrl: './schedule-meeting.modal.html',
  styleUrls: ['./schedule-meeting.modal.scss'],
})
export class ScheduleMeetingModal {
  private api = inject(MeetingsService);
  private usersApi = inject(UsersService);

  @Output() created = new EventEmitter<void>();
  @Output() updated = new EventEmitter<void>();

  mode = signal<Mode>('create');
  editingId: string | null = null;

  title = '';
  description = '';
  type: 'board' | 'committee' | 'emergency' | '' = '';
  date = '';
  start = '';
  end = '';
  location = '';

  provider: 'Zoom' | 'Microsoft365' = 'Microsoft365';
  hostIdentity: string | null = null;

  users = signal<MinimalUser[]>([]);
  totalUsers = signal<number>(0);
  page = signal<number>(1);
  pageSize = signal<number>(50);
  userQuery = signal<string>('');
  selected = signal<Set<string>>(new Set<string>());

  private attendeesRich: UpdateAttendeeDto[] = [];

  saving = false;
  error: string | null = null;

  private get modalInstance() {
    const el = document.getElementById('newMeetingModal');
    return el ? bootstrap.Modal.getOrCreateInstance(el) : null;
  }

  open(preset?: Partial<CreateMeetingDto>) {
    this.mode.set('create');
    this.editingId = null;
    this.attendeesRich = [];
    this.reset();
    if (preset) {
      this.title = preset.title ?? this.title;
      this.description = (preset.description ?? '') as string;
      this.type = (preset.type ?? '') as any;
      this.provider = (preset.provider ?? this.provider) as any;
      this.hostIdentity = (preset.hostIdentity ?? null) as any;

      if (preset.scheduledAt) {
        this.date = this.isoToLocalDateInput(preset.scheduledAt);
        this.start = this.isoToLocalTimeInput(preset.scheduledAt);
      }
      if (preset.endAt) {
        this.end = this.isoToLocalTimeInput(preset.endAt);
      }
    }
    this.page.set(1);
    this.userQuery.set('');
    this.loadUsers();
    this.modalInstance?.show();
  }

  openForEdit(m: MeetingDto) {
    this.mode.set('edit');
    this.editingId = m.id;
    this.error = null;
    this.saving = false;

    this.title = m.title ?? '';
    this.description = (m.description ?? '') as string;
    this.type = (m.type ?? '') as any;

    // Convert API ISO -> local inputs
    this.date = this.isoToLocalDateInput(m.scheduledAt);
    this.start = this.isoToLocalTimeInput(m.scheduledAt);

    if (m.endAt) {
      this.end = this.isoToLocalTimeInput(m.endAt);
    } else {
      const startLocal = new Date(m.scheduledAt);
      const endLocal = new Date(startLocal.getTime() + 60 * 60 * 1000);
      const hh = String(endLocal.getHours()).padStart(2, '0');
      const mm = String(endLocal.getMinutes()).padStart(2, '0');
      this.end = `${hh}:${mm}`;
    }

    this.location = m.location ?? 'TBD';
    this.provider = m.provider ?? 'Microsoft365';
    this.hostIdentity = m.hostIdentity ?? null;

    const existingUserIds = (m.attendees ?? [])
      .map((a) => a.userId)
      .filter((x): x is string => !!x);
    this.selected.set(new Set(existingUserIds));

    // hydrate attendeesRich including isRequired/isConfirmed
    this.attendeesRich = (m.attendees ?? []).map(
      (a: AttendeeDto): UpdateAttendeeDto => ({
        id: a.id,
        userId: a.userId ?? null,
        name: a.name,
        role: a.role ?? null,
        email: a.email ?? null,
        isRequired: a.isRequired ?? true,
        isConfirmed: a.isConfirmed ?? false,
      }),
    );

    this.page.set(1);
    this.userQuery.set('');
    this.loadUsers();
    this.modalInstance?.show();
  }

  close() {
    this.modalInstance?.hide();
  }

  loadUsers() {
    this.usersApi
      .getUsers({
        q: this.userQuery(),
        page: this.page(),
        pageSize: this.pageSize(),
        activeOnly: true,
      })
      .subscribe({
        next: (res) => {
          this.users.set(
            res.items.map((u) => ({
              id: u.id,
              name: u.fullName,
              email: u.email,
              roles: u.roles ?? [],
              isActive: u.isActive,
              departmentId: u.departmentId,
              departmentName: u.departmentName,
            })),
          );
          this.totalUsers.set(res.total);
        },
        error: () => {
          this.users.set([]);
          this.totalUsers.set(0);
        },
      });
  }

  clearSearch() {
    this.userQuery.set('');
    this.page.set(1);
    this.loadUsers();
  }
  nextPage() {
    this.page.set(this.page() + 1);
    this.loadUsers();
  }
  prevPage() {
    this.page.set(Math.max(1, this.page() - 1));
    this.loadUsers();
  }

  onUserCheckboxChange(id: string, ev: Event) {
    const checked = (ev.target as HTMLInputElement)?.checked ?? false;
    const s = new Set(this.selected());
    if (checked) s.add(id);
    else s.delete(id);
    this.selected.set(s);
  }

  // Convert local date+time to UTC ISO string
  private toUtcIso(dateStr: string, timeStr: string): string {
    const local = new Date(`${dateStr}T${timeStr}:00`);
    return local.toISOString();
  }

  // Convert ISO -> local YYYY-MM-DD
  private isoToLocalDateInput(iso: string): string {
    const d = new Date(iso);
    const yyyy = d.getFullYear();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
  }

  // Convert ISO -> local HH:mm
  private isoToLocalTimeInput(iso: string): string {
    const d = new Date(iso);
    const hh = String(d.getHours()).padStart(2, '0');
    const mm = String(d.getMinutes()).padStart(2, '0');
    return `${hh}:${mm}`;
  }

  private buildTimes(): { startIso: string; endIso: string } | null {
    if (!this.title || !this.date || !this.start) {
      this.error = 'Title, Date and Start time are required.';
      return null;
    }
    const startIso = this.toUtcIso(this.date, this.start);
    let endIso = this.end ? this.toUtcIso(this.date, this.end) : null;

    const startMs = new Date(startIso).getTime();
    if (!endIso || new Date(endIso).getTime() <= startMs) {
      endIso = new Date(startMs + 60 * 60 * 1000).toISOString();
    }

    return { startIso, endIso: endIso! };
  }

  reset() {
    this.title =
      this.description =
      this.type =
      this.date =
      this.start =
      this.end =
      this.location =
        '';
    this.provider = 'Microsoft365';
    this.hostIdentity = null;
    this.error = null;
    this.userQuery.set('');
    this.selected.set(new Set());
  }

  private handleConcurrencyOnUpdate(meetingId: string) {
    this.api.getById(meetingId).subscribe({
      next: (fresh) => {
        this.openForEdit(fresh);
        this.error =
          'This meeting was changed by someone else. I reloaded the latest data—please review and save again.';
      },
      error: () => {
        this.error =
          'Your data is stale and could not be refreshed automatically. Close and reopen to try again.';
      },
    });
  }

  save() {
    const times = this.buildTimes();
    if (!times) return;

    this.saving = true;
    this.error = null;

    const buildAttendeesRich = (): UpdateAttendeeDto[] => {
      const existingMap = new Map(this.attendeesRich.map(a => [a.userId ?? '', a]));
      const list: UpdateAttendeeDto[] = [];

      this.selected().forEach(userId => {
        const existing = existingMap.get(userId);
        const user = this.users().find(u => u.id === userId);

        if (existing) {
          // keep current flags
          list.push({
            id: existing.id,
            userId: existing.userId ?? userId,
            name: existing.name ?? user?.name ?? '',
            email: existing.email ?? user?.email ?? null,
            role: existing.role ?? null,
            isRequired: existing.isRequired ?? true,
            isConfirmed: existing.isConfirmed ?? false,
          });
        } else if (user) {
          // new attendee
          list.push({
            id: null,
            userId: user.id,
            name: user.name,
            email: user.email,
            role: null,
            isRequired: true,
            isConfirmed: false,
          });
        }
      });

      return list;
    };

    if (this.mode() === 'create') {
      const dto: CreateMeetingDto = {
        title: this.title.trim(),
        description: this.description?.trim() || null,
        type: (this.type || null) as any,
        scheduledAt: times.startIso,
        endAt: times.endIso,
        location: this.location?.trim() || 'TBD',
        attendeeUserIds: Array.from(this.selected()),
        provider: this.provider,
        hostIdentity: this.hostIdentity,
      };

      this.api.create(dto).subscribe({
        next: () => {
          this.saving = false;
          this.created.emit();
          this.modalInstance?.hide();
          this.reset();
        },
        error: (err) => {
          this.saving = false;
          const details = err?.error?.error?.details;
          if (details) {
            const msg = Object.entries(details)
              .flatMap(([k, arr]: any) => arr?.map((m: string) => `${k}: ${m}`) ?? [])
              .join(' | ');
            this.error = msg || 'Failed to create meeting.';
          } else {
            this.error = err?.error?.message || err?.message || 'Failed to create meeting.';
          }
        },
      });
    } else {
      const id = this.editingId!;
      const dto: UpdateMeetingDto = {
        id,
        title: this.title.trim(),
        description: this.description?.trim() || ' ',
        type: (this.type || null) as any,
        scheduledAt: times.startIso,
        endAt: times.endIso,
        location: this.location?.trim() || 'TBD',
        attendeeUserIds: Array.from(this.selected()),
        attendeesRich: buildAttendeesRich(),
      };

      this.api.update(id, dto).subscribe({
        next: () => {
          this.saving = false;
          this.updated.emit();
          this.modalInstance?.hide();
          this.reset();
        },
        error: (err) => {
          this.saving = false;
          const code = err?.error?.error?.code ?? err?.error?.code;
          if (err?.status === 409 || code === 'concurrency_conflict') {
            this.handleConcurrencyOnUpdate(id);
            return;
          }
          this.error = err?.error?.message || err?.message || 'Failed to update meeting.';
        },
      });
    }
  }
}
