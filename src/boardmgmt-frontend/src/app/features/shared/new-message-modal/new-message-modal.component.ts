import { Component, EventEmitter, Output, ViewChild, ElementRef, inject, signal, OnDestroy, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MessagesService } from '../../messages/messages.service';
import { CreateMessageRequest } from '../../messages/messages.models';
import { ToastrService } from 'ngx-toastr';
import { UsersService, MinimalUser } from '../../users/users.service';
import { Subject, Subscription, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, catchError, tap } from 'rxjs/operators';

declare const bootstrap: any;

@Component({
  standalone: true,
  selector: 'app-new-message-modal',
  imports: [CommonModule, FormsModule],
  templateUrl: './new-message-modal.component.html',
  styleUrls: ['./new-message-modal.component.scss'],
})
export class NewMessageModalComponent implements OnDestroy {
  private api = inject(MessagesService);
  private toast = inject(ToastrService);
  private usersApi = inject(UsersService);

  @Output() sent = new EventEmitter<void>();
  @ViewChild('modalEl') modalEl?: ElementRef<HTMLDivElement>;

  subject = signal('');
  priority = signal<'Low'|'Normal'|'High'|'Urgent'>('Normal');
  body = signal('');
  readReceipt = signal(false);
  confidential = signal(false);
  files: File[] = [];

  recipients = signal<MinimalUser[]>([]);
  searchText = signal('');
  suggestions = signal<MinimalUser[]>([]);
  searching = signal(false);

  searchBoxFocused = false;
  hoveringList = false;

  private term$ = new Subject<string>();
  private sub: Subscription;

  constructor() {
    this.sub = this.term$.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      tap(() => { this.searching.set(true); this.suggestions.set([]); }),
      switchMap(q => q?.trim().length >= 2 ? this.usersApi.search(q.trim()).pipe(catchError(() => of([]))) : of([])),
      tap(() => this.searching.set(false))
    ).subscribe((list) => {
      const selected = new Set(this.recipients().map(r => r.id));
      this.suggestions.set((list as MinimalUser[]).filter(u => !selected.has(u.id)));
    });
  }

  @HostListener('document:click')
  onDocClick() { if (!this.hoveringList) this.searchBoxFocused = false; }

  onSearchInput(q: string) {
    this.searchText.set(q);
    this.searchBoxFocused = true;
    this.term$.next(q);
  }

  addRecipient(u: MinimalUser) {
    if (!this.recipients().some(r => r.id === u.id)) {
      this.recipients.set([...this.recipients(), u]);
    }
    this.searchText.set('');
    this.suggestions.set([]);
  }

  removeRecipient(userId: string) {
    this.recipients.set(this.recipients().filter(r => r.id !== userId));
  }

  open(prefill?: Partial<CreateMessageRequest>) {
    if (prefill?.recipientIds?.length) {
      const merged = [...this.recipients()];
      for (const id of prefill.recipientIds) {
        if (!merged.some(r => r.id === id)) merged.push({ id, name: id, email: id });
      }
      this.recipients.set(merged);
    }
    if (prefill?.subject) this.subject.set(prefill.subject);
    if (prefill?.body) this.body.set(prefill.body);
    if (prefill?.priority) this.priority.set(prefill.priority as any);

    const el = this.modalEl?.nativeElement; if (!el) return;
    bootstrap.Modal.getOrCreateInstance(el).show();
  }

  close() {
    const el = this.modalEl?.nativeElement; if (!el) return;
    bootstrap.Modal.getOrCreateInstance(el).hide();
  }

  onFiles(ev: Event) {
    const input = ev.target as HTMLInputElement;
    this.files = Array.from(input.files ?? []);
  }

  clear() {
    this.recipients.set([]);
    this.subject.set(''); this.priority.set('Normal'); this.body.set('');
    this.readReceipt.set(false); this.confidential.set(false); this.files = [];
    this.searchText.set(''); this.suggestions.set([]);
  }

  private recipientIds(): string[] {
    return this.recipients().map(r => r.id);
  }

  saveDraft() {
    const req: CreateMessageRequest = {
      subject: this.subject(), body: this.body(), priority: this.priority(),
      recipientIds: this.recipientIds(), readReceiptRequested: this.readReceipt(),
      isConfidential: this.confidential(), asDraft: true
    };
    this.api.create(req).subscribe({
      next: () => { this.toast.success('Draft saved'); this.close(); this.clear(); },
      error: () => this.toast.error('Failed to save draft'),
    });
  }

  sendMessage() {
    const recipientIds = this.recipientIds();
    if (!recipientIds.length) { this.toast.warning('Please select at least one recipient'); return; }

    const req: CreateMessageRequest = {
      subject: this.subject(), body: this.body(), priority: this.priority(),
      recipientIds, readReceiptRequested: this.readReceipt(), isConfidential: this.confidential(),
      asDraft: false
    };
    this.api.create(req).subscribe({
      next: (res: any) => {
        const id = res?.id;
        const afterUpload = () => {
          this.api.send(id).subscribe({
            next: () => { this.toast.success('Message sent'); this.close(); this.clear(); this.sent.emit(); },
            error: () => { this.toast.success('Message created; send failed (retry from Drafts)'); this.sent.emit(); }
          });
        };
        if (id && this.files.length) {
          this.api.uploadAttachments(id, this.files).subscribe({ next: afterUpload, error: afterUpload });
        } else afterUpload();
      },
      error: () => this.toast.error('Failed to create message'),
    });
  }

  setSubject(v: string) { this.subject.set(v ?? ''); }
  setPriority(v: 'Low'|'Normal'|'High'|'Urgent') { this.priority.set(v ?? 'Normal'); }
  setBody(v: string) { this.body.set(v ?? ''); }
  setReadReceipt(v: boolean) { this.readReceipt.set(!!v); }
  setConfidential(v: boolean) { this.confidential.set(!!v); }

  ngOnDestroy() { this.sub?.unsubscribe(); }
}
