import { CommonModule, NgClass, NgFor, NgIf } from '@angular/common';
import { Component, Input, signal, computed, inject } from '@angular/core';
import { DashboardService } from '../../dashboard/dashboard.service';
import {
  StatsKind,
  PagedResult,
  MeetingItem,
  DocumentItem,
  VoteItem,
  ActiveUserItem,
} from '../../dashboard/dashboard.api';

@Component({
  standalone: true,
  selector: 'app-stats-detail-modal',
  imports: [CommonModule, NgIf, NgFor, NgClass],
  templateUrl: './stats-detail-modal.component.html',
  styleUrls: ['./stats-detail-modal.component.scss'],
})
export class StatsDetailModalComponent {
  private api = inject(DashboardService);

  kind = signal<StatsKind>('meetings');
  title = computed(() => {
    switch (this.kind()) {
      case 'meetings':
        return 'Upcoming Meetings';
      case 'documents':
        return 'Active Documents';
      case 'votes':
        return 'Pending Votes';
      case 'users':
        return 'Active Users';
    }
  });

  // pagination
  page = signal(1);
  pageSize = signal(10);

  // data
  loading = signal(false);
  error = signal<string | null>(null);
  total = signal(0);

  // union list
  meetings = signal<MeetingItem[]>([]);
  documents = signal<DocumentItem[]>([]);
  votes = signal<VoteItem[]>([]);
  users = signal<ActiveUserItem[]>([]); 

  // modal show/hide (bootstrap-like minimal)
  visible = signal(false);

  open(kind: StatsKind) {
    this.kind.set(kind);
    this.page.set(1);
    this.visible.set(true);
    this.load();
  }
  close() {
    this.visible.set(false);
  }

  next() {
    if (this.page() * this.pageSize() >= this.total()) return;
    this.page.update((p) => p + 1);
    this.load();
  }
  prev() {
    if (this.page() === 1) return;
    this.page.update((p) => p - 1);
    this.load();
  }

  private load() {
    this.loading.set(true);
    this.error.set(null);

    this.api.getStatsDetail(this.kind(), this.page(), this.pageSize()).subscribe({
      next: (res) => {
        this.total.set(res.totalCount);
        // clear lists
        this.meetings.set([]);
        this.documents.set([]);
        this.votes.set([]);
          this.users.set([]); 

        switch (this.kind()) {
          case 'meetings':
            this.meetings.set(res.items as MeetingItem[]);
            break;
          case 'documents':
            this.documents.set(res.items as DocumentItem[]);
            break;
          case 'votes':
            this.votes.set(res.items as VoteItem[]);
            break;
          case 'users':
            this.users.set(res.items as ActiveUserItem[]); break;
            break;
        }
        this.loading.set(false);
      },
      error: (e) => {
        console.error(e);
        this.error.set('Failed to load details.');
        this.loading.set(false);
      },
    });
  }
}
