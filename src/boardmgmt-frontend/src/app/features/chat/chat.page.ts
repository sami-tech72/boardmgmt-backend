import {
  Component,
  OnInit,
  OnDestroy,
  ViewChild,
  ElementRef,
  signal,
  computed,
  inject,
  PLATFORM_ID,
} from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { Router } from '@angular/router';

import { ChatService } from './chat.service';
import { UsersService, MinimalUser, UserDto } from '../../features/users/users.service';
import {
  ChatMessageDto,
  ConversationDetailDto,
  ConversationListItemDto,
  MinimalUserDto,
} from './chat.models';
import { environment } from '../../../environments/environment';
import { BROWSER_STORAGE, StorageLike } from '@core/tokens/browser-storage.token';

/** JWT helpers */
function b64UrlDecode(input: string): string {
  let s = input.replace(/-/g, '+').replace(/_/g, '/');
  const pad = s.length % 4;
  if (pad) s += '='.repeat(4 - pad);
  return typeof atob !== 'undefined' ? atob(s) : Buffer.from(s, 'base64').toString('binary');
}
function decodeJwt(token: string | null): Record<string, any> | null {
  if (!token) return null;
  const parts = token.split('.');
  if (parts.length < 2) return null;
  try {
    return JSON.parse(b64UrlDecode(parts[1]));
  } catch {
    return null;
  }
}
function userIdFromToken(token: string | null): string {
  const c = decodeJwt(token);
  if (!c) return '';
  return (
    c['sub'] ||
    c['nameid'] ||
    c['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ||
    ''
  );
}

@Component({
  standalone: true,
  selector: 'app-chat',
  imports: [CommonModule, FormsModule],
  templateUrl: './chat.page.html',
  styleUrls: ['./chat.page.scss'],
})
export class ChatPage implements OnInit, OnDestroy {
  private chat = inject(ChatService);
  private usersApi = inject(UsersService);
  private toast = inject(ToastrService);
  private storage: StorageLike = inject(BROWSER_STORAGE);
  private platformId = inject(PLATFORM_ID);
  private router = inject(Router);

  /** Current user id from JWT (SSR-safe via injected storage) */
  get selfId(): string {
    return userIdFromToken(this.storage.getItem('jwt'));
  }

  channels = signal<ConversationListItemDto[]>([]);
  directs = signal<ConversationListItemDto[]>([]);
  detail = signal<ConversationDetailDto | null>(null);

  activeId = signal<string | null>(null);
  messages = signal<ChatMessageDto[]>([]);
  draft = '';
  files: File[] = [];
  searchQ = '';
  hover: string | null = null;

  // thread
  threadRoot = signal<ChatMessageDto | null>(null);
  threadMsgs = signal<ChatMessageDto[]>([]);
  threadDraft = '';

  // typing & presence
  typingUsers = signal<string[]>([]);
  onlineMap = new Map<string, boolean>();

  // People
  people = signal<MinimalUser[]>([]);
  peopleFilter = '';
  activePersonId: string | null = null;

  filteredPeople = computed(() => {
    const q = (this.peopleFilter || '').toLowerCase().trim();
    if (!q) return this.people();
    return this.people().filter(
      (u) => (u.name || '').toLowerCase().includes(q) || (u.email || '').toLowerCase().includes(q),
    );
  });

  @ViewChild('scrollHost') scrollHost?: ElementRef<HTMLDivElement>;

  ngOnInit() {
    if (!isPlatformBrowser(this.platformId)) return;

    const uid = this.selfId;
    if (!uid) {
      this.router.navigateByUrl('/login');
      return;
    }

    this.chat.connect(uid);

    // Load People
    this.usersApi.getAllUsersFlat().subscribe({
      next: (list: UserDto[]) => {
        const mapped: MinimalUser[] = list.map((u) => ({
          id: u.id,
          name: (u.fullName || u.email || u.id) + (u.id === uid ? ' (you)' : ''),
          email: u.email,
        }));
        this.people.set(mapped);
      },
      error: (err) => this.toast.error(`Failed to load people (${err?.status || 'err'})`),
    });

    this.loadConversations();

    // ---- Live events ----
    this.chat.messageCreated$.subscribe((e) => {
      if (!e || e.conversationId !== this.activeId()) return;

      const msg = e.message;
      if (!msg) return;

      if (msg.threadRootId) {
        this.applyThreadRootUpdate(e.threadRoot ?? null);
        this.applyMessageToThread(msg);
      } else {
        this.applyMessageToConversation(msg);
        if (this.threadRoot()?.id === msg.id) {
          this.threadRoot.set(this.mergeMessage(this.threadRoot(), msg));
        }
        setTimeout(() => {
          this.scrollToBottomIfNear();
          this.markReadSoon();
        }, 0);
      }
    });

    this.chat.messageEdited$.subscribe((e) => {
      if (!e || e.conversationId !== this.activeId()) return;

      const msg = e.message;
      if (!msg) return;

      if (msg.threadRootId) {
        this.applyThreadRootUpdate(e.threadRoot ?? null);
        this.applyMessageToThread(msg);
      } else {
        this.applyMessageToConversation(msg);
        if (this.threadRoot()?.id === msg.id) {
          this.threadRoot.set(this.mergeMessage(this.threadRoot(), msg));
        }
      }
    });

    this.chat.messageDeleted$.subscribe((e) => {
      if (!e || e.conversationId !== this.activeId()) return;

      const msg = e.message;
      if (!msg) return;

      if (msg.threadRootId) {
        this.applyThreadRootUpdate(e.threadRoot ?? null);
        this.applyMessageToThread(msg);
      } else {
        this.applyMessageToConversation(msg);
        if (this.threadRoot()?.id === msg.id) {
          this.threadRoot.set(this.mergeMessage(this.threadRoot(), msg));
        }
      }
    });

    this.chat.reactionUpdated$.subscribe((e) => {
      if (!e) return;
      if (e.conversationId !== this.activeId()) return;

      const updateList = (list: ChatMessageDto[]) =>
        list.map((m) => (m.id === e.messageId ? { ...m, reactions: e.reactions } : m));

      const nextMessages = updateList(this.messages());
      this.messages.set(nextMessages);

      const root = this.threadRoot();
      if (root) {
        if (root.id === e.messageId) {
          this.threadRoot.set({ ...root, reactions: e.reactions });
        }
        if (e.threadRootId && root.id === e.threadRootId) {
          this.threadMsgs.set(updateList(this.threadMsgs()));
        }
      }
    });

    this.chat.typing$.subscribe((t) => {
      if (!t || t.conversationId !== this.activeId()) return;
      const names = new Set(this.typingUsers());
      // (Optional) map t.userId to a name if you have it
      if (t.isTyping) names.add('Someone');
      else names.delete('Someone');
      this.typingUsers.set([...names]);
      setTimeout(() => {
        names.delete('Someone');
        this.typingUsers.set([...names]);
      }, 1500);
    });
  }

  ngOnDestroy() {}

  // Conversations
  loadConversations() {
    this.chat.listConversations().subscribe({
      next: (list) => {
        this.channels.set(list.filter((x) => x.type === 'Channel'));
        this.directs.set(list.filter((x) => x.type !== 'Channel'));
        if (!this.activeId() && list.length) this.open(list[0].id);
      },
      error: (err) => this.toast.error(`Failed to load conversations (${err?.status || 'err'})`),
    });
  }

  open(id: string) {
    if (this.activeId() === id) return;
    if (this.activeId()) this.chat.leaveConversation(this.activeId()!);
    this.activeId.set(id);
    this.detail.set(null);
    this.messages.set([]);
    this.threadRoot.set(null);
    this.threadMsgs.set([]);

    this.chat.getConversation(id).subscribe({ next: (d) => this.detail.set(d) });
    this.chat.joinConversation(id);
    this.loadMore();
  }

  openWithUser(u: MinimalUser) {
    if (u.id === this.selfId) {
      this.toast.info('This is you 🙂');
      return;
    }
    this.activePersonId = u.id;
    this.chat.startDirect(u.id).subscribe({
      next: (res) => {
        if (res?.id) {
          this.open(res.id);
          this.loadConversations();
        }
      },
      error: () => this.toast.error('Failed to start direct message'),
    });
  }

  // Paging & scrolling
  private oldest(): string | undefined {
    const arr = this.messages();
    return arr.length ? arr[0].createdAtUtc : undefined;
    // NOTE: API returns newest-first; we reverse when merging to keep ascending in UI
  }

  loadMore() {
    const id = this.activeId();
    if (!id) return;
    this.chat.history(id, this.oldest(), 50).subscribe({
      next: (p) => {
        const prepend = [...p.items].reverse();
        const merged = [...prepend, ...this.messages()];
        const map = new Map(merged.map((m) => [m.id, m]));
        this.messages.set([...map.values()]);
        setTimeout(() => {
          this.scrollToBottomIfNear();
          this.markReadSoon();
        }, 0);
      },
      error: () => this.toast.error('Failed to load history'),
    });
  }

  /** Fetch only the newest message(s) and append */
  refreshLatest() {
    const id = this.activeId();
    if (!id) return;

    this.chat.history(id, undefined, 50).subscribe({
      next: (p) => {
        const incoming = [...p.items].reverse(); // make ascending for UI
        if (!incoming.length) return;

        const merged = [...this.messages(), ...incoming];
        const map = new Map<string, ChatMessageDto>();
        for (const msg of merged) {
          map.set(msg.id, msg);
        }
        const next = Array.from(map.values()).sort((a, b) =>
          a.createdAtUtc.localeCompare(b.createdAtUtc),
        );

        this.messages.set(next);
        setTimeout(() => {
          this.scrollToBottomIfNear();
          this.markReadSoon();
        }, 0);
      },
      error: () => this.toast.error('Failed to refresh'),
    });
  }

  private mergeMessage(
    prev: ChatMessageDto | null | undefined,
    incoming: ChatMessageDto,
  ): ChatMessageDto {
    if (!prev) return incoming;

    const reactedMap = new Map(prev.reactions?.map((r) => [r.emoji, r.reactedByMe]));
    const reactions = incoming.reactions?.map((r) => ({
      ...r,
      reactedByMe: reactedMap.get(r.emoji) ?? r.reactedByMe,
    }));

    return {
      ...prev,
      ...incoming,
      reactions: reactions ?? incoming.reactions,
    };
  }

  private applyMessageToConversation(msg: ChatMessageDto) {
    if (msg.threadRootId) return;
    const current = this.messages();
    const idx = current.findIndex((m) => m.id === msg.id);
    const merged = this.mergeMessage(idx >= 0 ? current[idx] : null, msg);

    let next = idx >= 0 ? [...current] : [...current, merged];
    if (idx >= 0) next[idx] = merged;

    next = [...next].sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc));
    this.messages.set(next);
  }

  private applyMessageToThread(msg: ChatMessageDto) {
    const root = this.threadRoot();
    if (!root || msg.threadRootId !== root.id) return;

    const current = this.threadMsgs();
    const idx = current.findIndex((m) => m.id === msg.id);
    const merged = this.mergeMessage(idx >= 0 ? current[idx] : null, msg);

    let next = idx >= 0 ? [...current] : [...current, merged];
    if (idx >= 0) next[idx] = merged;

    next = [...next].sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc));
    this.threadMsgs.set(next);
  }

  private applyThreadRootUpdate(root: ChatMessageDto | null) {
    if (!root) return;
    this.applyMessageToConversation(root);
    const currentRoot = this.threadRoot();
    if (currentRoot && currentRoot.id === root.id) {
      this.threadRoot.set(this.mergeMessage(currentRoot, root));
    }
  }

  onScroll() {
    const el = this.scrollHost?.nativeElement;
    if (!el) return;
    if (el.scrollTop < 80) this.loadMore();
  }
  private scrollToBottomIfNear() {
    const el = this.scrollHost?.nativeElement;
    if (!el) return;
    const near = el.scrollHeight - el.scrollTop - el.clientHeight < 240;
    if (near) el.scrollTop = el.scrollHeight;
  }
  private markReadSoon() {
    const id = this.activeId();
    if (!id) return;
    this.chat.markRead(id).subscribe();
  }

  // Helpers
  displayName(u?: MinimalUserDto | null) {
    return u?.fullName || u?.email || 'Unknown';
  }
  byId(_i: number, m: ChatMessageDto) {
    return m.id;
  }
  isMine(m: ChatMessageDto) {
    return (m.fromUser?.id ?? '') === this.selfId;
  }

  // Composer
  onFiles(ev: Event) {
    const input = ev.target as HTMLInputElement;
    this.files = Array.from(input.files ?? []);
  }
  pickEmoji(e: string) {
    this.draft += ` ${e} `;
  }
  onTyping(_inThread = false) {
    const id = this.activeId();
    if (!id) return;
    this.chat.setTyping(id, true).subscribe();
  }
  renderMessage(plain: string) {
    const esc = plain.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    const at = esc.replace(/@([\w.\-]+)/g, '<span class="mention">@$1</span>');
    return at.replace(/\n/g, '<br/>');
  }
  send() {
    const id = this.activeId();
    if (!id) return;
    const html = this.renderMessage(this.draft.trim());
    if (!html) return;
    this.chat.send(id, html).subscribe({
      next: (res) => {
        const msgId = res.id;
        const after = () => {
          this.draft = '';
          this.files = [];
          setTimeout(() => this.scrollToBottomIfNear(), 0);
        };
        if (msgId && this.files.length)
          this.chat.uploadAttachments(msgId, this.files).subscribe({ next: after, error: after });
        else after();
      },
      error: () => this.toast.error('Send failed'),
    });
  }

  // Actions
  editMsg(m: ChatMessageDto) {
    const current = m.bodyHtml.replace(/<br\/?>/g, '\n').replace(/<[^>]+>/g, '');
    const next = prompt('Edit message:', current);
    if (next === null) return;
    const html = this.renderMessage(next);
    this.chat.edit(m.id, html).subscribe({
      next: () => this.toast.success('Edited'),
      error: () => this.toast.error('Edit failed'),
    });
  }

  deleteMsg(m: ChatMessageDto) {
    if (!confirm('Delete this message?')) return;
    this.chat.remove(m.id).subscribe({
      next: () => this.toast.success('Deleted'),
      error: () => this.toast.error('Delete failed'),
    });
  }

  toggleReaction(m: ChatMessageDto, emoji: string) {
    const mine = m.reactions?.find((r) => r.emoji === emoji)?.reactedByMe;
    (mine ? this.chat.removeReaction(m.id, emoji) : this.chat.addReaction(m.id, emoji)).subscribe({
      error: () => this.toast.error('Reaction failed'),
    });
  }

  // Threads
  openThread(m: ChatMessageDto) {
    this.threadRoot.set(m);
    this.threadMsgs.set([]);
    const id = this.activeId();
    if (!id) return;
    this.chat.history(id, undefined, 50, m.id).subscribe({
      next: (p) => this.threadMsgs.set(p.items.reverse()),
      error: () => this.toast.error('Failed to load thread'),
    });
  }
  private reloadThread() {
    const root = this.threadRoot();
    const id = this.activeId();
    if (!root || !id) return;
    this.chat.history(id, undefined, 50, root.id).subscribe({
      next: (p) => this.threadMsgs.set(p.items.reverse()),
      error: () => this.toast.error('Failed to load thread'),
    });
  }
  closeThread() {
    this.threadRoot.set(null);
    this.threadMsgs.set([]);
  }
  sendThread() {
    const root = this.threadRoot();
    const convId = this.activeId();
    if (!root || !convId) return;
    const html = this.renderMessage(this.threadDraft.trim());
    if (!html) return;
    this.chat.send(convId, html, root.id).subscribe({
      next: () => {
        this.threadDraft = '';
        this.reloadThread();
      },
      error: () => this.toast.error('Thread send failed'),
    });
  }

  // Attachments
  attachmentUrl(messageId: string, attachmentId: string) {
    return `${environment.apiUrl}/chat/messages/${messageId}/attachments/${attachmentId}`;
  }

  // Search
  doSearch() {
    const q = (this.searchQ ?? '').trim();
    if (!q) return;
    this.chat.search(q, 50).subscribe({
      next: (hits) => {
        if (!hits.length) {
          this.toast.info('No results');
          return;
        }
        this.open(hits[0].conversationId);
      },
      error: () => this.toast.error('Search failed'),
    });
  }

  openCreateChannel() {
    const name = (prompt('Channel name (without #):') || '').trim();
    if (!name) return;
    const isPrivate = confirm('Make this a private channel?');
    this.chat.createChannel(name, isPrivate, []).subscribe({
      next: (res) => {
        this.loadConversations();
        if (res?.id) this.open(res.id);
      },
      error: () => this.toast.error('Create channel failed'),
    });
  }
}
