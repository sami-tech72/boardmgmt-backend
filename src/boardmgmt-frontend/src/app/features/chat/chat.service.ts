import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BROWSER_STORAGE } from '@core/tokens/browser-storage.token';
import {
  ChatMessageDto,
  ConversationDetailDto,
  ConversationListItemDto,
  PagedResult,
  MessageCreatedEvent,
  MessageEditedEvent,
  MessageDeletedEvent,
  ReactionUpdatedEvent,
  TypingEvent,
} from './chat.models';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private http = inject(HttpClient);
  private storage = inject(BROWSER_STORAGE);

  private base = `${environment.apiUrl}/chat`;
  // Backend hub is mapped to /hubs/chat => env.hubUrl+"/chat"
  private hubUrl = `${environment.hubUrl}/chat`;

  private httpOpts = { withCredentials: true } as const;

  private hub?: signalR.HubConnection;
  private startPromise?: Promise<void>;

  // Live events (raw payloads from server)
  messageCreated$ = new BehaviorSubject<MessageCreatedEvent | null>(null);
  messageEdited$ = new BehaviorSubject<MessageEditedEvent | null>(null);
  messageDeleted$ = new BehaviorSubject<MessageDeletedEvent | null>(null);
  reactionUpdated$ = new BehaviorSubject<ReactionUpdatedEvent | null>(null);
  typing$ = new BehaviorSubject<TypingEvent | null>(null);

  connect(userId: string) {
    if (this.hub) return;

    this.hub = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        withCredentials: true,
        accessTokenFactory: () => this.storage.getItem('jwt') || '',
      })
      .withAutomaticReconnect()
      .build();

    // wire events
    this.hub.on('MessageCreated', (p: MessageCreatedEvent) => this.messageCreated$.next(p));
    this.hub.on('MessageEdited', (p: MessageEditedEvent) => this.messageEdited$.next(p));
    this.hub.on('MessageDeleted', (p: MessageDeletedEvent) => this.messageDeleted$.next(p));
    this.hub.on('ReactionUpdated', (p: ReactionUpdatedEvent) => this.reactionUpdated$.next(p));
    this.hub.on('Typing', (p: TypingEvent) => this.typing$.next(p));

    this.startPromise = this.hub
      .start()
      .then(async () => {
        await this.hub!.invoke('JoinUser', userId);
      })
      .catch((err) => console.error('[SignalR] start failed:', err));

    this.hub.onreconnected(async () => {
      try {
        await this.hub!.invoke('JoinUser', userId);
      } catch {}
    });
  }

  private async ensureConnected() {
    if (!this.startPromise) throw new Error('Hub not started');
    await this.startPromise;
  }

  async joinConversation(conversationId: string) {
    await this.ensureConnected();
    await this.hub?.invoke('JoinConversation', conversationId);
  }
  async leaveConversation(conversationId: string) {
    if (!this.hub) return;
    await this.hub.invoke('LeaveConversation', conversationId);
  }

  // Conversations
  listConversations(): Observable<ConversationListItemDto[]> {
    return this.http.get<ConversationListItemDto[]>(`${this.base}/conversations`, this.httpOpts);
  }
  getConversation(id: string): Observable<ConversationDetailDto> {
    return this.http.get<ConversationDetailDto>(`${this.base}/conversations/${id}`, this.httpOpts);
  }
  createChannel(name: string, isPrivate: boolean, memberIds: string[]) {
    return this.http.post<{ id: string }>(
      `${this.base}/channels`,
      { name, isPrivate, memberIds },
      this.httpOpts,
    );
  }
  join(conversationId: string) {
    return this.http.post(`${this.base}/conversations/${conversationId}/join`, {}, this.httpOpts);
  }
  leave(conversationId: string) {
    return this.http.post(`${this.base}/conversations/${conversationId}/leave`, {}, this.httpOpts);
  }

  // Read state
  markRead(conversationId: string, readAtUtc?: string) {
    const body = readAtUtc ? JSON.stringify(readAtUtc) : 'null';
    const headers = new HttpHeaders({ 'Content-Type': 'application/json' });
    return this.http.post<boolean>(`${this.base}/conversations/${conversationId}/read`, body, {
      ...this.httpOpts,
      headers,
    });
  }

  // Direct message bootstrap
  startDirect(userId: string) {
    return this.http.post<{ id: string }>(
      `${this.base}/direct/${encodeURIComponent(userId)}`,
      {},
      this.httpOpts,
    );
  }

  // History & messages
  history(conversationId: string, beforeUtc?: string, take = 50, threadRootId?: string) {
    const params = new HttpParams().set('beforeUtc', beforeUtc ?? '').set('take', take);
    if (threadRootId) {
      return this.http.get<PagedResult<ChatMessageDto>>(`${this.base}/threads/${threadRootId}`, {
        params,
        ...this.httpOpts,
      });
    }
    return this.http.get<PagedResult<ChatMessageDto>>(
      `${this.base}/conversations/${conversationId}/history`,
      { params, ...this.httpOpts },
    );
  }

  send(conversationId: string, bodyHtml: string, threadRootId?: string) {
    return this.http.post<{ id: string }>(
      `${this.base}/conversations/${conversationId}/messages`,
      { bodyHtml, threadRootId },
      this.httpOpts,
    );
  }
  edit(messageId: string, bodyHtml: string) {
    return this.http.put(`${this.base}/messages/${messageId}`, { bodyHtml }, this.httpOpts);
  }
  remove(messageId: string) {
    return this.http.delete(`${this.base}/messages/${messageId}`, this.httpOpts);
  }

  // Reactions
  addReaction(messageId: string, emoji: string) {
    const headers = new HttpHeaders({ 'Content-Type': 'application/json' });
    return this.http.post(`${this.base}/messages/${messageId}/reactions`, JSON.stringify(emoji), {
      ...this.httpOpts,
      headers,
    });
  }
  removeReaction(messageId: string, emoji: string) {
    const headers = new HttpHeaders({ 'Content-Type': 'application/json' });
    return this.http.request('DELETE', `${this.base}/messages/${messageId}/reactions`, {
      body: JSON.stringify(emoji),
      headers,
      ...this.httpOpts,
    });
  }

  // Typing
  setTyping(conversationId: string, isTyping: boolean) {
    const headers = new HttpHeaders({ 'Content-Type': 'application/json' });
    return this.http.post(
      `${this.base}/conversations/${conversationId}/typing`,
      JSON.stringify(isTyping),
      { ...this.httpOpts, headers },
    );
  }

  // Attachments
  uploadAttachments(messageId: string, files: File[]) {
    const fd = new FormData();
    for (const f of files) fd.append('files', f, f.name);
    return this.http.post(`${this.base}/messages/${messageId}/attachments`, fd, this.httpOpts);
  }

  // Search
  search(term: string, take = 50) {
    const params = new HttpParams().set('term', term).set('take', take);
    return this.http.get<ChatMessageDto[]>(`${this.base}/search`, { params, ...this.httpOpts });
  }
}
