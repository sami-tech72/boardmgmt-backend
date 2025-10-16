import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '@env/environment';

export enum VoteType {
  YesNo = 0,
  ApproveReject = 1,
  MultipleChoice = 2,
}
export enum VoteEligibility {
  Public = 0,
  MeetingAttendees = 1,
  SpecificUsers = 2,
}
export enum VoteChoice {
  Yes = 1,
  No = 2,
  Abstain = 3,
}

export type VoteOptionDto = {
  id: string;
  text: string;
  order: number;
  count: number;
};

export type VoteResultsDto = {
  totalBallots: number;
  yes: number;
  no: number;
  abstain: number;
  options: VoteOptionDto[];
};

export type VoteSummaryDto = {
  id: string;
  title: string;
  description?: string | null;
  type: VoteType;
  deadline: string;
  isOpen: boolean;
  eligibility: VoteEligibility;
  results: VoteResultsDto;
  alreadyVoted: boolean;        // from backend
  myChoice?: VoteChoice | null; // NEW
  myOptionId?: string | null;   // NEW
};

export interface IndividualVoteDto {
  userId: string;
  displayName: string;
  choiceLabel?: string | null;
  optionText?: string | null;
  votedAt: string;
}

export type VoteDetailDto = {
  id: string;
  meetingId?: string | null;
  agendaItemId?: string | null;
  title: string;
  description?: string | null;
  type: VoteType;
  allowAbstain: boolean;
  anonymous: boolean;
  createdAt: string;
  deadline: string;
  eligibility: VoteEligibility;
  options: VoteOptionDto[];
  results: VoteResultsDto;
  canVote: boolean;
  alreadyVoted: boolean;
  individualVotes?: IndividualVoteDto[] | null;
};

export type CreateVoteRequest = {
  title: string;
  description?: string | null;
  type: VoteType;
  allowAbstain: boolean;
  anonymous: boolean;
  deadline: string;
  eligibility: VoteEligibility;
  meetingId?: string | null;
  agendaItemId?: string | null;
  options?: string[] | null;
  specificUserIds?: string[] | null;
};

export type MeetingMinimalDto = {
  id: string;
  title: string;
  scheduledAt: string;
};

export type EligibleVoterDto = {
  id: string;
  name: string;
  role?: string;
  userId?: string;
};

export type UserSearchDto = {
  id: string;
  userName?: string | null;
  email?: string | null;
  displayName?: string | null;
};

@Injectable({ providedIn: 'root' })
export class VotingService {
  private http = inject(HttpClient);

  private readonly base =
    (typeof window !== 'undefined' && (window as any)?.env?.apiUrl) ||
    environment.apiUrl ||
    'https://localhost:44325/api';

  listActive(): Observable<VoteSummaryDto[]> {
    return this.http.get<VoteSummaryDto[]>(`${this.base}/votes/active`);
  }

  // listRecent(): Observable<VoteSummaryDto[]> {
  //   return this.http.get<VoteSummaryDto[]>(`${this.base}/votes/recent`);
  // }
  listRecent(): Observable<VoteSummaryDto[]> {
  // Interceptor adds Authorization for you
  return this.http.get<VoteSummaryDto[]>(`${this.base}/votes/recent`);
}


  getOne(id: string): Observable<VoteDetailDto> {
    return this.http.get<VoteDetailDto>(`${this.base}/votes/${id}`);
  }

  createVote(body: CreateVoteRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/votes`, body);
  }

  // IMPORTANT: backend returns VoteSummaryDto now
 submitBallot(
  voteId: string,
  body: { choice?: VoteChoice | null; optionId?: string | null }
): Observable<VoteSummaryDto> {
  return this.http.post<VoteSummaryDto>(`${this.base}/votes/${voteId}/ballots`, body);
}

  listMeetingsMinimal(): Observable<MeetingMinimalDto[]> {
    return this.http.get<MeetingMinimalDto[]>(`${this.base}/meetings/select-list`);
  }

  listEligibleVoters(meetingId: string): Observable<EligibleVoterDto[]> {
    return this.http
      .get(`${this.base}/meetings/${encodeURIComponent(meetingId)}`)
      .pipe(
        map((body: any) => (body?.data ?? body) as any),
        map((mtg: any) => {
          const attendees = Array.isArray(mtg?.attendees) ? mtg.attendees : [];
          return attendees.map((a: any) => ({
            id: a.id,
            name: a.name,
            role: a.role ?? null,
            userId: a.userId ?? null,
          })) as EligibleVoterDto[];
        })
      );
  }

  searchUsers(query: string): Observable<UserSearchDto[]> {
    const params = new HttpParams().set('q', query ?? '');
    return this.http
      .get(`${this.base}/users/search`, { params })
      .pipe(map((body: any) => (Array.isArray(body) ? body : (body?.data ?? body?.items ?? []))));
  }
}
