export interface DashboardStats {
  upcomingMeetings: number;
  activeDocuments: number;
  pendingVotes: number;
  activeUsers: number;
}

export interface DashboardMeeting {
  id: string;
  title: string;
  subtitle?: string | null;
  startsAt: string; // ISO string
  status: 'Upcoming' | 'Completed' | 'Cancelled';
}

export interface DashboardDocument {
  id: string;
  title: string;
  kind: 'pdf' | 'word' | 'excel' | 'ppt' | 'other';
  updatedAgo: string; // e.g., "2 hours ago"
}

export interface DashboardActivity {
  id: string;
  kind: 'upload' | 'meeting_completed' | 'vote_reminder' | 'generic';
  title: string;
  text: string;
  whenAgo: string;
  color?: 'primary' | 'success' | 'warning' | 'info' | 'danger';
}

export interface DashboardDataEnvelope<T> {
  success: boolean;
  result: T;
  message?: string;
}
