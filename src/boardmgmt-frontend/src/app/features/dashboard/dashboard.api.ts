// Defines shared API types/enums for dashboard
export type StatsKind = 'meetings' | 'documents' | 'votes' | 'users';

export interface PagedResult<T> {
  totalCount: number;
  items: T[];
  page: number;
  pageSize: number;
}

export interface MeetingItem {
  id: string;
  title: string;
  subtitle?: string | null;
  startsAtUtc: string; // ISO
  status: 'Upcoming' | 'Completed' | 'Cancelled';
}

export interface DocumentItem {
  id: string;
  title: string;
  kind: 'pdf' | 'word' | 'excel' | 'ppt' | 'other';
  updatedAgo: string;
}

export interface VoteItem {
  id: string;
  title: string;
  deadlineUtc: string; // ISO
}



export interface ActiveUserItem {
  id: string;
  displayName: string;
  email?: string | null;
  lastSeenUtc?: string | null; // ISO (optional)
}
