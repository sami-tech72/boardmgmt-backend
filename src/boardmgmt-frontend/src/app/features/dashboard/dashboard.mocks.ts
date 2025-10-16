import { DashboardActivity, DashboardDocument, DashboardMeeting, DashboardStats } from './dashboard.models';

export const MOCK_STATS: DashboardStats = {
  upcomingMeetings: 3,
  activeDocuments: 24,
  pendingVotes: 2,
  activeUsers: 7,
};

export const MOCK_MEETINGS: DashboardMeeting[] = [
  {
    id: 'm1',
    title: 'Board of Directors Meeting',
    subtitle: 'Quarterly Review',
    startsAt: new Date().toISOString(),
    status: 'Upcoming',
  },
  {
    id: 'm2',
    title: 'Finance Committee',
    subtitle: 'Budget Planning',
    startsAt: new Date(Date.now() - 24 * 3600 * 1000).toISOString(),
    status: 'Completed',
  },
  {
    id: 'm3',
    title: 'Strategic Planning',
    subtitle: '2025 Roadmap',
    startsAt: new Date(Date.now() - 2 * 24 * 3600 * 1000).toISOString(),
    status: 'Completed',
  },
];

export const MOCK_DOCUMENTS: DashboardDocument[] = [
  { id: 'd1', title: 'Q4 Financial Report', kind: 'pdf',   updatedAgo: '2 hours ago' },
  { id: 'd2', title: 'Meeting Minutes',     kind: 'word',  updatedAgo: '1 day ago' },
  { id: 'd3', title: 'Budget Analysis',     kind: 'excel', updatedAgo: '3 days ago' },
];

export const MOCK_ACTIVITY: DashboardActivity[] = [
  { id: 'a1', kind: 'upload',            title: 'New document uploaded', text: 'Q4 Financial Report has been uploaded by Sarah Johnson', whenAgo: '2 hours ago', color: 'primary' },
  { id: 'a2', kind: 'meeting_completed', title: 'Meeting completed',     text: 'Finance Committee meeting has been completed',          whenAgo: '1 day ago',   color: 'success' },
  { id: 'a3', kind: 'vote_reminder',     title: 'Vote reminder',         text: 'Reminder: Budget approval vote ends in 2 days',         whenAgo: '2 days ago',  color: 'warning' },
];
