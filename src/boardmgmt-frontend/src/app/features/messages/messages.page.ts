import { Component, OnInit, ViewChild, signal, inject } from '@angular/core';
import { CommonModule, NgIf, NgFor, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MessagesService } from './messages.service';
import {
  MessageListItemDto,
  MessageDetailDto,
  CreateMessageRequest,
  MessageThreadDto,
  MessageBubble,
  MinimalUserDto,
} from './messages.models';
import { NewMessageModalComponent } from '../shared/new-message-modal/new-message-modal.component';
import { PageHeaderComponent } from '../shared/page-header/page-header.component';
import { ToastrService } from 'ngx-toastr';

@Component({
  standalone: true,
  selector: 'app-messages',
  imports: [CommonModule, FormsModule, NgIf, NgFor, NgClass, NewMessageModalComponent, PageHeaderComponent],
  templateUrl: './messages.page.html',
  styleUrls: ['./messages.page.scss'],
})
export class MessagesPage implements OnInit {
  private api = inject(MessagesService);
  private toast = inject(ToastrService);

  public getUserId(): string | undefined {
    return localStorage.getItem('userId') ?? undefined;
  }
  public me(): string {
    return this.getUserId() ?? '';
  }

  inbox = signal<MessageListItemDto[]>([]);
  total = signal(0);
  loading = signal(false);
  selectedId = signal<string | null>(null);
  detail = signal<MessageDetailDto | null>(null);
  thread = signal<MessageThreadDto | null>(null);

  q = '';
  priorityFilter = '';
  statusFilter = '';
  page = signal(1);
  pageSize = signal(20);
  replyText = signal('');

  @ViewChild('composeModal') composeModal?: NewMessageModalComponent;

  ngOnInit() {
    const uid = this.me();
    if (uid) this.api.connect(uid);
    this.api.newMessage$.subscribe((p) => {
      if (p) this.load(true);
    });
    this.api.threadAppended$.subscribe((p) => {
      if (p && this.selectedId() === p.anchorId) this.reloadThread();
    });
    this.load();
  }
  ngOnDestroy() {
    this.api.disconnect();
  }

  load(keepSelection = false) {
    this.loading.set(true);
    this.api
      .list({
        q: this.q,
        priority: this.priorityFilter,
        status: this.statusFilter,
        page: this.page(),
        pageSize: this.pageSize(),
        forUserId: this.getUserId(),
      })
      .subscribe({
        next: (res) => {
          const grouped = this.groupIntoConversations(res.items);
          this.inbox.set(grouped);
          this.total.set(grouped.length);
          if (!keepSelection && !this.selectedId() && grouped.length) this.select(grouped[0].id);
        },
        error: () => {
          this.inbox.set([]);
          this.total.set(0);
          this.toast.error('Failed to load messages');
        },
        complete: () => this.loading.set(false),
      });
  }

  reloadThread() {
    const id = this.selectedId();
    if (!id) return;
    this.api.get(id).subscribe({ next: (d) => this.detail.set(d) });
    this.api.getThread(id).subscribe({ next: (t) => this.thread.set(t) });
  }

  select(id: string) {
    this.selectedId.set(id);
    this.detail.set(null);
    this.thread.set(null);
    this.api.get(id).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.api.markRead(id).subscribe();
      },
      error: () => this.toast.error('Failed to load message'),
    });
    this.api.getThread(id).subscribe({ next: (t) => this.thread.set(t) });
  }

  deleteSelected() {
    const id = this.selectedId();
    if (!id) return;
    this.api.delete(id).subscribe({
      next: () => {
        this.toast.success('Message deleted');
        this.selectedId.set(null);
        this.load();
      },
      error: () => this.toast.error('Delete failed'),
    });
  }
  openCompose(prefill?: Partial<CreateMessageRequest>) {
    this.composeModal?.open(prefill);
  }
  onSentRefresh() {
    this.load(true);
  }

  private normalizeSubject(s: string) {
    let x = (s ?? '').trim();
    const re = /^(re|fwd)\s*:\s*/i;
    while (re.test(x)) x = x.replace(re, '').trim();
    return x.toLowerCase();
  }
  private otherPartyKey(item: MessageListItemDto): string {
    const fromId = item.fromUser?.id ? String(item.fromUser.id) : 'unknown';
    const mine = this.me();
    return fromId === mine ? 'me→others' : fromId;
  }
  private groupIntoConversations(items: MessageListItemDto[]) {
    const map = new Map<string, MessageListItemDto>();
    for (const it of items) {
      const key = `${this.normalizeSubject(it.subject)}|${this.otherPartyKey(it)}`;
      const existing = map.get(key);
      map.set(
        key,
        !existing
          ? it
          : new Date(it.updatedAtUtc) > new Date(existing.updatedAtUtc)
          ? it
          : existing,
      );
    }
    return Array.from(map.values()).sort(
      (a, b) => +new Date(b.updatedAtUtc) - +new Date(a.updatedAtUtc),
    );
  }

  whoLabel(from?: MinimalUserDto | null): string {
    const mine = this.me();
    if (!from?.id) return 'Unknown';
    return String(from.id) === mine ? 'You' : from.fullName || from.email || 'Unknown';
  }
  isOutgoingBubble(b?: MessageBubble | null): boolean {
    if (!b?.fromUser?.id) return false;
    return String(b.fromUser.id) === this.me();
  }
  recipientsLine(d: MessageDetailDto): string {
    if (!d?.recipients?.length) return '(no recipients)';
    const mine = this.me();
    const names = d.recipients
      .filter((r) => String(r.id) !== mine)
      .map((r) => r.fullName || r.email || r.id);
    return names.length ? names.join(', ') : 'me';
  }

  replyPrefill(d: MessageDetailDto): Partial<CreateMessageRequest> {
    const toId = d.fromUser?.id ? String(d.fromUser.id) : '';
    return {
      subject: `Re: ${d.subject}`,
      recipientIds: toId ? [toId] : [],
      body: this.quote(d),
      priority: d.priority,
    };
  }
  replyAllPrefill(d: MessageDetailDto): Partial<CreateMessageRequest> {
    const me = this.getUserId();
    const others = new Set<string>();
    d.recipients.forEach((r) => {
      if (String(r.id) !== me) others.add(String(r.id));
    });
    if (d.fromUser?.id && String(d.fromUser.id) !== me) others.add(String(d.fromUser.id));
    return {
      subject: `Re: ${d.subject}`,
      recipientIds: Array.from(others),
      body: this.quote(d),
      priority: d.priority,
    };
  }
  forwardPrefill(d: MessageDetailDto): Partial<CreateMessageRequest> {
    return {
      subject: `Fwd: ${d.subject}`,
      recipientIds: [],
      body: this.quote(d),
      priority: d.priority,
    };
  }
  private quote(d: MessageDetailDto) {
    const from = d.fromUser?.fullName || d.fromUser?.email || 'Unknown';
    const when = new Date(d.createdAtUtc).toLocaleString();
    return `\n\n---- Original message ----\nFrom: ${from}\nSent: ${when}\nSubject: ${d.subject}\n\n${d.body}`;
  }

  sendReply() {
    const d = this.detail();
    const text = this.replyText().trim();
    if (!d || !text) return;
    const to = d.fromUser?.id ? [String(d.fromUser.id)] : [];
    if (!to.length) {
      this.toast.warning('No reply target found');
      return;
    }
    const req: CreateMessageRequest = {
      subject: `Re: ${d.subject}`,
      body: text,
      priority: d.priority ?? 'Normal',
      recipientIds: to,
      readReceiptRequested: true,
      asDraft: false,
    };
    this.api.create(req).subscribe({
      next: (res) => {
        const id = (res as any)?.id;
        const afterUpload = () =>
          this.api.send(id).subscribe({
            next: () => {
              this.toast.success('Reply sent');
              this.replyText.set('');
              this.reloadThread();
            },
            error: () => this.toast.error('Send failed'),
          });
        if (id) afterUpload();
      },
      error: () => this.toast.error('Failed to create reply'),
    });
  }

  attachmentUrl(messageId: string, attachmentId?: string) {
    return attachmentId ? this.api.attachmentUrl(messageId, attachmentId) : '#';
  }
}
