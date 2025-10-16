import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

export enum MeetingStatus {
  Draft = 0,
  Scheduled = 1,
  Completed = 2,
  Cancelled = 3,
}
export type MeetingType = 'board' | 'committee' | 'emergency';

const TypeNumToLabel: Record<number, MeetingType> = { 0: 'board', 1: 'committee', 2: 'emergency' };
const TypeLabelToNum: Record<MeetingType, number> = { board: 0, committee: 1, emergency: 2 };

export interface AttendeeDto {
  id: string;
  name: string;
  email?: string | null;
  role?: string | null;
  userId?: string | null;
  isRequired?: boolean;
  isConfirmed?: boolean;
}

export interface UpdateAttendeeDto {
  id: string | null;            // null => new attendee
  userId?: string | null;       // keep to help server map by user when creating
  name: string;
  role?: string | null;
  email?: string | null;
  isRequired: boolean;
  isConfirmed: boolean;
}

export interface MeetingDto {
  id: string;
  title: string;
  description?: string | null;
  type?: MeetingType | null;
  scheduledAt: string;
  endAt?: string | null;
  location: string;
  status: MeetingStatus;
  attendeesCount: number;
  agendaItems?: { id: string; title: string; description?: string | null; order: number }[];
  attendees?: AttendeeDto[];
  joinUrl?: string | null;
  provider?: 'Zoom' | 'Microsoft365' | null;
  hostIdentity?: string | null;
}

interface ApiMeetingDto extends Omit<MeetingDto, 'type'> { type?: number | null; }

export interface CreateMeetingDto {
  title: string;
  description?: string | null;
  type?: MeetingType | null;
  scheduledAt: string;
  endAt?: string | null;
  location: string;
  attendeeUserIds?: string[];
  attendees?: string[] | null;
  provider: 'Zoom' | 'Microsoft365';
  hostIdentity?: string | null;
}

interface ApiCreateMeetingDto extends Omit<CreateMeetingDto, 'type'> { type?: number | null; }

export interface UpdateMeetingDto {
  id: string;
  title: string;
  description?: string | null;
  type?: MeetingType | null;
  scheduledAt: string;
  endAt?: string | null;
  location: string;
  attendeeUserIds?: string[];
  attendeesRich?: UpdateAttendeeDto[];
}

interface ApiUpdateMeetingDto extends Omit<UpdateMeetingDto, 'type'> { type?: number | null; }

export interface TranscriptUtteranceDto {
  start: number | string;
  end: number | string;
  text: string;
  speakerName?: string | null;
  speakerEmail?: string | null;
  userId?: string | null;
}

export interface TranscriptDto {
  id: string;
  provider: 'Zoom' | 'Microsoft365';
  providerTranscriptId?: string | null;
  createdUtc: string;
  utterances: TranscriptUtteranceDto[];
}

@Injectable({ providedIn: 'root' })
export class MeetingsService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/meetings`;

  private toUi = (m: ApiMeetingDto): MeetingDto => ({
    ...m,
    type: m.type == null ? null : TypeNumToLabel[m.type] ?? null,
  });

  private toApiCreate = (dto: CreateMeetingDto): ApiCreateMeetingDto => ({
    ...dto,
    type: dto.type == null ? null : TypeLabelToNum[dto.type],
  });

  private toApiUpdate = (dto: UpdateMeetingDto): ApiUpdateMeetingDto => ({
    ...dto,
    type: dto.type == null ? null : TypeLabelToNum[dto.type],
  });

  getAll(): Observable<MeetingDto[]> {
    return this.http.get<ApiMeetingDto[]>(this.base).pipe(map((list) => list.map(this.toUi)));
  }

  getById(id: string): Observable<MeetingDto> {
    return this.http.get<ApiMeetingDto>(`${this.base}/${id}`).pipe(map(this.toUi));
  }

  create(dto: CreateMeetingDto) {
    return this.http.post<{ id: string }>(this.base, this.toApiCreate(dto));
  }

  update(id: string, dto: UpdateMeetingDto) {
    return this.http.put<{ id: string }>(`${this.base}/${id}`, this.toApiUpdate({ ...dto, id }));
  }

  listForPicker() {
    return this.http.get<Array<{ id: string; title: string; scheduledAt: string }>>(`${this.base}/select-list`);
  }

  getTranscript(meetingId: string) {
    return this.http.get<TranscriptDto>(`${this.base}/${meetingId}/transcripts`);
  }

  ingestTranscript(meetingId: string) {
    return this.http.post<{ meetingId: string; utterances: number }>(`${this.base}/${meetingId}/transcripts/ingest`, {});
  }
}
