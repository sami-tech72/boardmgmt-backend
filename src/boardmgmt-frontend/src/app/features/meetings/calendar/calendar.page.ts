import {
  Component,
  ViewChild,
  computed,
  effect,
  inject,
  signal,
  ViewEncapsulation,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import {
  CalendarView,
  CalendarEvent,
  CalendarModule,
  CalendarEventAction,
  CalendarEventTimesChangedEvent,
} from 'angular-calendar';

import { Subject } from 'rxjs';

import { DragAndDropModule } from 'angular-draggable-droppable';

import { CalendarApiService, CalendarEventDto } from './calendar-api.service';
import { ScheduleMeetingModal } from '../../shared/schedule-meeting-modal/schedule-meeting.modal';
import { MeetingsService, MeetingDto } from '../../meetings/meetings.service';
import { UserMenuComponent } from '@app/features/shared/user-menu/user-menu.component';
import { PageHeaderComponent } from '@app/features/shared/page-header/page-header.component';

type Provider = 'Zoom' | 'Microsoft365' | 'All';
type EventMeta = { id: string; joinUrl?: string | null; provider?: Provider | null };
type UiEvent = CalendarEvent<EventMeta>;

const colors = {
  red: { primary: '#ad2121', secondary: '#FAE3E3' },
  blue: { primary: '#1e90ff', secondary: '#D1E8FF' },
  yellow: { primary: '#e3bc08', secondary: '#FDF1BA' },
} as const;

@Component({
  standalone: true,
  selector: 'app-calendar',
  imports: [
    CommonModule,
    FormsModule,
    CalendarModule,
    DragAndDropModule,
    ScheduleMeetingModal,
    UserMenuComponent,
    PageHeaderComponent,
  ],
  templateUrl: './calendar.page.html',
  styleUrls: ['./calendar.page.scss'],
  encapsulation: ViewEncapsulation.None,
})
export class CalendarPage {
  private api = inject(CalendarApiService);
  private meetings = inject(MeetingsService);

  @ViewChild(ScheduleMeetingModal) createModal!: ScheduleMeetingModal;

  // view + toolbar
  view: CalendarView = CalendarView.Month;
  CalendarView = CalendarView;
  viewDate = signal<Date>(new Date());
  provider = signal<Provider>('All');

  // state
  loading = signal<boolean>(false);
  error = signal<string | null>(null);
  refresh$ = new Subject<void>();

  activeDayIsOpen = signal<boolean>(false);

  // backing data
  raw = signal<CalendarEventDto[]>([]);

  actions: CalendarEventAction[] = [
    {
      label: '<i class="fas fa-fw fa-pencil-alt"></i>',
      a11yLabel: 'Edit',
      onClick: ({ event }: { event: UiEvent }) => this.onEventClicked(event),
    },
    {
      label: '<i class="fas fa-fw fa-trash-alt"></i>',
      a11yLabel: 'Delete',
      onClick: ({ event }: { event: UiEvent }) => this.deleteEvent(event),
    },
  ];

  // map DTO -> CalendarEvent with styling + meta
  events = computed<UiEvent[]>(() =>
    this.raw().map<UiEvent>((e) => {
      const start = new Date(e.startUtc);
      const end = new Date(e.endUtc);
      const provider = (e.provider ?? null) as Provider | null;

      const css =
        provider === 'Zoom' ? 'cal-zoom' : provider === 'Microsoft365' ? 'cal-m365' : 'cal-generic';

      const color =
        provider === 'Zoom'
          ? colors.blue
          : provider === 'Microsoft365'
          ? colors.yellow
          : colors.red;

      return {
        start,
        end,
        title: e.subject || '(no subject)',
        cssClass: css,
        color,
        actions: this.actions,
        draggable: true,
        resizable: { beforeStart: true, afterEnd: true },
        meta: { id: e.id, joinUrl: e.joinUrl ?? null, provider },
      };
    }),
  );

  constructor() {
    effect(() => {
      const current = this.viewDate();
      const prov = this.provider();
      const { start, end } = this.getRangeForView(current, this.view);
      this.fetch(start, end, prov);
    });
  }

  /* Toolbar actions */
  setView(view: CalendarView) {
    this.view = view;
    this.reloadAfterCreate();
  }
  today() {
    this.viewDate.set(new Date());
  }
  prev() {
    this.viewDate.set(this.addByView(this.viewDate(), -1));
  }
  next() {
    this.viewDate.set(this.addByView(this.viewDate(), +1));
  }
  onProviderChange(v: string) {
    this.provider.set(v as Provider);
  }

  /* Quick create on day click (optional) */
  private _clickTimer: any = null;
  private _lastClick: { time: number; date: Date } | null = null;
  private readonly _dblClickMs = 250;

  onMonthDayClicked(date: Date) {
    const now = Date.now();
    if (this._lastClick && now - this._lastClick.time < this._dblClickMs) {
      if (this._clickTimer) clearTimeout(this._clickTimer);
      this._clickTimer = null;
      this._lastClick = null;
      return; // ignore double-click
    }
    this._lastClick = { time: now, date };
    this._clickTimer = setTimeout(() => {
      this._clickTimer = null;
      this._lastClick = null;
      this.createAtDay(date);
    }, this._dblClickMs);
  }

  /* CREATE */
  onHourSegmentClicked(date: Date) {
    const start = new Date(date);
    const end = new Date(date);
    end.setHours(start.getHours() + 1);
    this.openCreatePreset(start, end);
  }
  createAtDay(date: Date) {
    const start = new Date(date);
    start.setHours(9, 0, 0, 0);
    const end = new Date(start);
    end.setHours(start.getHours() + 1);
    this.openCreatePreset(start, end);
  }
  private openCreatePreset(start: Date, end: Date) {
    const preset = {
      title: '',
      description: '',
      type: undefined,
      provider: 'Microsoft365' as const,
      hostIdentity: null,
    };
    this.createModal.open(preset);
    this.createModal.date = start.toISOString().slice(0, 10);
    this.createModal.start = start.toISOString().slice(11, 16);
    this.createModal.end = end.toISOString().slice(11, 16);
  }

  /* JOIN */
  join(event: UiEvent) {
    const link = event.meta?.joinUrl;
    if (link) window.open(link, '_blank', 'noopener,noreferrer');
  }

  /* EDIT */
  onEventClicked(event: UiEvent) {
    const id = event.meta?.id;
    if (!id) return;
    this.meetings.getById(id).subscribe({
      next: (m: MeetingDto) => this.createModal.openForEdit(m),
      error: (err) => console.error('Failed to load meeting for edit', id, err),
    });
  }

  /* DELETE */
  deleteEvent(event: UiEvent) {
    const id = event.meta?.id;
    if (!id) return;

    const backup = this.raw();
    this.raw.set(backup.filter((x) => x.id !== id));
    this.refresh$.next();

    this.api.delete(id).subscribe({
      next: () => {},
      error: (e) => {
        console.error(e);
        this.raw.set(backup);
        this.refresh$.next();
      },
    });
  }

  /**
   * Month drag→drop handler.
   * Keeps the original time-of-day from the drag source,
   * moves the event to the dropped day, and preserves duration.
   */
  eventDropped(day: any, event?: CalendarEvent, draggedFrom?: Date) {
    if (!event) return;

    const from = draggedFrom ?? event.start;
    const origStart = new Date(from);
    const origEnd = new Date(event.end ?? from);
    const durationMs = Math.max(0, origEnd.getTime() - origStart.getTime());

    // newStart = dropped calendar day with original time-of-day
    const newStart = new Date(day.date);
    newStart.setHours(
      origStart.getHours(),
      origStart.getMinutes(),
      origStart.getSeconds(),
      origStart.getMilliseconds(),
    );

    const newEnd = durationMs ? new Date(newStart.getTime() + durationMs) : undefined;

    this.eventTimesChanged({
      event: event as UiEvent,
      newStart,
      newEnd,
    } as CalendarEventTimesChangedEvent);
  }

  /* Drag / Resize (built-in from angular-calendar) */
  eventTimesChanged({ event, newStart, newEnd }: CalendarEventTimesChangedEvent) {
    const id = (event as UiEvent).meta?.id;
    if (!id) return;

    const start = newStart ?? event.start;
    const end = newEnd ?? event.end ?? start;

    // optimistic UI
    const updated = this.raw().map((dto) =>
      dto.id === id ? { ...dto, startUtc: start.toISOString(), endUtc: end!.toISOString() } : dto,
    );
    this.raw.set(updated);
    this.refresh$.next();

    // persist
    this.api.moveEvent(id, start, end!).subscribe({
      next: () => {},
      error: (err) => {
        console.error('Move failed', err);
        this.reloadAfterCreate();
      },
    });
  }

  /* reload after create/update */
  reloadAfterCreate() {
    const { start, end } = this.getRangeForView(this.viewDate(), this.view);
    this.fetch(start, end, this.provider());
  }

  /* data fetch */
  private fetch(start: Date, end: Date, _provider: Provider) {
    this.loading.set(true);
    this.error.set(null);
    this.api.getRangeFromDb(start, end).subscribe({
      next: (list) => {
        this.raw.set(list);
        this.loading.set(false);
        this.refresh$.next();
      },
      error: (err) => {
        const msg = err?.error?.message || err?.message || 'Failed to load calendar';
        this.error.set(msg);
        this.loading.set(false);
      },
    });
  }

  /* helpers: ranges */
  private getRangeForView(center: Date, view: CalendarView) {
    const first = (dt: Date) => new Date(dt.getFullYear(), dt.getMonth(), 1);
    const last = (dt: Date) => new Date(dt.getFullYear(), dt.getMonth() + 1, 0, 23, 59, 59, 999);

    if (view === CalendarView.Month) return { start: first(center), end: last(center) };

    if (view === CalendarView.Week) {
      const day = center.getDay();
      const diffToMon = (day + 6) % 7; // Monday=0
      const start = new Date(center);
      start.setDate(center.getDate() - diffToMon);
      start.setHours(0, 0, 0, 0);
      const end = new Date(start);
      end.setDate(start.getDate() + 6);
      end.setHours(23, 59, 59, 999);
      return { start, end };
    }

    const start = new Date(center);
    start.setHours(0, 0, 0, 0);
    const end = new Date(center);
    end.setHours(23, 59, 59, 999);
    return { start, end };
  }

  private addByView(d: Date, step: number) {
    const nd = new Date(d);
    if (this.view === CalendarView.Month) nd.setMonth(d.getMonth() + step);
    else if (this.view === CalendarView.Week) nd.setDate(d.getDate() + step * 7);
    else nd.setDate(d.getDate() + step);
    return nd;
  }

  onLogout(): void {
    // optional: extra cleanup or message
    console.log('User logged out from dashboard');
    // No need to navigate manually — UserMenuComponent already does that.
  }
}
