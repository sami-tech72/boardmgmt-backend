import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  MessageListItemDto,
  MessageDetailDto,
  MessageThreadDto,
  PagedResult,
  CreateMessageRequest,
  MessageAttachmentDto,
} from './messages.models';
import * as signalR from '@microsoft/signalr';

@Injectable({ providedIn: 'root' })
export class MessagesService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/messages`;
  private hubUrl = `${environment.hubUrl}/messages`;
  private httpOpts = { withCredentials: true } as const;

  private hub?: signalR.HubConnection;
  newMessage$ = new BehaviorSubject<{ id: string } | null>(null);
  threadAppended$ = new BehaviorSubject<{ anchorId: string } | null>(null);
  readReceipt$ = new BehaviorSubject<{ messageId: string; userId: string } | null>(null);

  connect(userId: string) {
    if (this.hub) return;
    this.hub = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    this.hub.on('NewMessage', (p: { id: string }) => this.newMessage$.next(p));
    this.hub.on('ThreadAppended', (p: { anchorId: string }) => this.threadAppended$.next(p));
    this.hub.on('ReadReceipt', (p: { messageId: string; userId: string }) =>
      this.readReceipt$.next(p),
    );

    this.hub
      .start()
      .then(() => this.hub?.invoke('JoinUser', userId))
      .catch(console.error);
  }
  disconnect() {
    this.hub?.stop().catch(() => {});
    this.hub = undefined;
  }

  list(params: {
    q?: string;
    priority?: string;
    status?: string;
    page?: number;
    pageSize?: number;
    forUserId?: string;
    sentByUserId?: string;
  }): Observable<PagedResult<MessageListItemDto>> {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(params))
      if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v));
    return this.http.get<PagedResult<MessageListItemDto>>(this.base, {
      params: p,
      ...this.httpOpts,
    });
  }

  get(id: string): Observable<MessageDetailDto> {
    return this.http.get<MessageDetailDto>(`${this.base}/${id}`, this.httpOpts);
  }
  getThread(id: string): Observable<MessageThreadDto> {
    return this.http.get<MessageThreadDto>(`${this.base}/${id}/thread`, this.httpOpts);
  }
  create(body: CreateMessageRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.base, body, this.httpOpts);
  }
  send(id: string) {
    return this.http.post<{ id: string }>(`${this.base}/${id}/send`, {}, this.httpOpts);
  }
  update(id: string, body: Partial<CreateMessageRequest>) {
    return this.http.put<{ id: string }>(`${this.base}/${id}`, body, this.httpOpts);
  }
  delete(id: string) {
    return this.http.delete<{ id: string }>(`${this.base}/${id}`, this.httpOpts);
  }

  uploadAttachments(messageId: string, files: File[]): Observable<MessageAttachmentDto[]> {
    const fd = new FormData();
    for (const f of files) fd.append('files', f, f.name);
    return this.http.post<MessageAttachmentDto[]>(
      `${this.base}/${messageId}/attachments`,
      fd,
      this.httpOpts,
    );
  }

  markRead(id: string) {
    return this.http.post<{ id: string }>(`${this.base}/${id}/read`, {}, this.httpOpts);
  }
  attachmentUrl(messageId: string, attachmentId: string) {
    return `${this.base}/${messageId}/attachments/${attachmentId}`;
  }
}
