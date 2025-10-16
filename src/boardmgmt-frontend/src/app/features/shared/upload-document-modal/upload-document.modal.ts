import {
  Component,
  EventEmitter,
  Output,
  ViewChild,
  ElementRef,
  inject,
  Inject,
  PLATFORM_ID,
  NgZone,
} from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import {
  DocumentsService,
  FolderDto,
  RoleItem,
  DocumentDto,
} from '@core/services/documents.service';

declare global {
  interface Window {
    bootstrap?: any;
  }
}

@Component({
  standalone: true,
  selector: 'app-upload-document-modal',
  imports: [CommonModule, FormsModule],
  templateUrl: './upload-document.modal.html',
  styleUrls: ['./upload-document.modal.scss'],
})
export class UploadDocumentModal {
  listsLoaded = false;
  private api = inject(DocumentsService);
  private zone = inject(NgZone);

  constructor(@Inject(PLATFORM_ID) private platformId: Object) {}

  @Output() uploaded = new EventEmitter<void>();
  @ViewChild('modalEl', { static: true }) modalEl!: ElementRef<HTMLDivElement>;

  mode: 'create' | 'edit' = 'create';
  editingDoc: DocumentDto | null = null;

  files: File[] = [];
  folderSlug = 'root';
  description = '';
  meetingId: string | '' = '';

  folders: FolderDto[] = [];
  meetings: { id: string; title: string; scheduledAt: string }[] = [];
  roles: RoleItem[] = [];
  selected: Record<string, boolean> = {};

  progress = 0;
  uploading = false;
  fileError = '';
  submitError = '';
  dragOver = false;

  private modalRef: any;

  // Only run data fetches in the browser, not during SSR
  ngOnInit() {
    if (!isPlatformBrowser(this.platformId)) return;

    const Modal = (window as any).bootstrap?.Modal;
    if (Modal) {
      this.modalRef = new Modal(this.modalEl.nativeElement, { backdrop: 'static' });

      // when modal is actually shown, refresh lists again (ensures cookies are present)
      this.modalEl.nativeElement.addEventListener('shown.bs.modal', () => {
        this.zone.run(() => this.refreshLists());
      });
    }

    // initial fetch (browser only)
    this.refreshLists();
  }

  openForCreate() {
    this.mode = 'create';
    this.editingDoc = null;
    this.resetStateForOpen();
    this.modalRef?.show();
  }

  openForEdit(doc: DocumentDto) {
    this.mode = 'edit';
    this.editingDoc = doc;

    this.resetStateForOpen();

    // Preselect from the doc (normalize)
    this.folderSlug = (doc.folderSlug ?? '').trim() || 'root';
    this.description = doc.description || '';

    this.modalRef?.show();
  }

  private resetStateForOpen() {
    this.progress = 0;
    this.uploading = false;
    this.fileError = '';
    this.submitError = '';
    this.files = [];
    // keep slug in edit; default to root in create
    this.folderSlug = this.mode === 'edit' ? this.folderSlug || 'root' : 'root';
    if (this.mode !== 'edit') this.description = '';
    this.meetingId = '';

    // Don’t fetch during SSR; modal 'shown' will refetch too
    if (isPlatformBrowser(this.platformId)) this.refreshLists();
  }

  // Robust folder/roles/meetings fetching with normalization & re-assertion
  async refreshLists() {
    this.listsLoaded = false;
    try {
      const arr = await firstValueFrom(this.api.listFolders());
      const raw = arr || [];

      this.folders = raw.map((x) => ({
        ...x,
        slug: (x.slug ?? '').trim() || 'root',
      }));

      // Re-assert selection once options exist
      if (this.mode === 'edit' && this.editingDoc) {
        const wanted = (this.editingDoc.folderSlug ?? '').trim() || 'root';
        const match = this.folders.find((f) => f.slug === wanted);
        this.folderSlug = match ? match.slug : 'root';
      } else if (this.mode === 'create') {
        this.folderSlug = (this.folderSlug ?? '').trim() || 'root';
      }

      // Fetch meetings & roles
      this.api.listMeetings().subscribe((m) => (this.meetings = m || []));
      this.api.listRoles().subscribe((roles) => {
        this.roles = (roles || []).slice();
        this.selected = this.buildDefaultSelection(this.roles);
      });
    } finally {
      // Flip after we've kicked off child fetches; the folder select is ready now.
      this.listsLoaded = true;
    }
  }

  private buildDefaultSelection(roles: RoleItem[]): Record<string, boolean> {
    const sel: Record<string, boolean> = {};
    const anyDefault = roles.some((r) => r.isDefault === true);
    if (anyDefault) {
      for (const r of roles) sel[r.id] = !!r.isDefault;
      return sel;
    }
    const admin = roles.find((r) => r.name === 'Admin');
    const board = roles.find((r) => r.name === 'BoardMember');
    if (admin || board) for (const r of roles) sel[r.id] = r === admin || r === board;
    else for (let i = 0; i < roles.length; i++) sel[roles[i].id] = i < 2;
    return sel;
  }

  onDragOver(evt: DragEvent) {
    evt.preventDefault();
    this.dragOver = true;
  }
  onDragLeave(evt: DragEvent) {
    evt.preventDefault();
    this.dragOver = false;
  }
  onDrop(evt: DragEvent) {
    evt.preventDefault();
    this.dragOver = false;
    let list = Array.from(evt.dataTransfer?.files || []);
    if (this.mode === 'edit') list = list.slice(0, 1);
    if (list.length) this.appendFiles(list);
  }
  onFileChange(evt: any) {
    let list: File[] = Array.from(evt?.target?.files ?? []);
    if (this.mode === 'edit') list = list.slice(0, 1);
    this.appendFiles(list);
  }
  removeFile(i: number) {
    this.files.splice(i, 1);
    this.validateFiles();
  }

  private appendFiles(list: File[]) {
    const dedup = new Map(this.files.map((f) => [f.name + '|' + f.size, f]));
    for (const f of list) dedup.set(f.name + '|' + f.size, f);
    this.files = Array.from(dedup.values());
    if (this.mode === 'edit' && this.files.length > 1) this.files = [this.files[0]];
    this.validateFiles();
  }

  private validateFiles() {
    this.fileError = '';
    const MAX = 50 * 1024 * 1024;
    const allowed = [
      'application/pdf',
      'application/msword',
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      'application/vnd.ms-excel',
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      'application/vnd.ms-powerpoint',
      'application/vnd.openxmlformats-officedocument.presentationml.presentation',
    ];
    for (const f of this.files) {
      if (f.size > MAX) {
        this.fileError = `File "${f.name}" exceeds 50MB limit.`;
        break;
      }
      if (f.type && !allowed.includes(f.type)) {
        const okExt = /\.(pdf|docx?|xlsx?|pptx?)$/i.test(f.name);
        if (!okExt) {
          this.fileError = `File "${f.name}" is not an allowed type.`;
          break;
        }
      }
    }
  }

  prettySize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    const kb = bytes / 1024;
    if (kb < 1024) return `${kb.toFixed(1)} KB`;
    const mb = kb / 1024;
    if (mb < 1024) return `${mb.toFixed(1)} MB`;
    const gb = mb / 1024;
    return `${gb.toFixed(1)} GB`;
  }

  private getSelectedRoleIds(): string[] {
    const ids: string[] = [];
    for (const r of this.roles) if (this.selected[r.id]) ids.push(r.id);
    return ids;
  }

  submit() {
    if (this.mode === 'create') return this.submitCreate();
    return this.submitEdit();
  }

  // CREATE -> POST /api/documents (multipart)
  private submitCreate() {
    if (!this.files.length || this.fileError) return;
    this.uploading = true;
    this.submitError = '';
    this.progress = 0;

    const roleIds = this.getSelectedRoleIds();
    const payloadRoleIds = roleIds.length ? roleIds : undefined;

    this.api
      .createDocuments(this.files, {
        meetingId: this.meetingId || null,
        folderSlug: this.folderSlug,
        description: this.description || undefined,
        roleIds: payloadRoleIds,
      })
      .subscribe({
        next: (ev: any) => {
          if (ev?.type === 1 && ev.total) this.progress = Math.round((ev.loaded * 100) / ev.total);
          if (ev?.type === 4) {
            this.uploading = false;
            this.modalRef?.hide();
            this.uploaded.emit();
          }
        },
        error: (e) => {
          this.uploading = false;
          this.submitError = e?.error?.message || e?.message || 'Upload failed.';
        },
      });
  }

  // UPDATE (metadata + optional file) -> PUT /api/documents/{id}/form (multipart)
  private submitEdit() {
    if (!this.editingDoc) return;
    this.uploading = true;
    this.submitError = '';
    this.progress = 0;

    const roleIds = this.getSelectedRoleIds();
    const meta = {
      originalName: this.editingDoc.originalName,
      description: this.description?.trim() || null,
      folderSlug: this.folderSlug?.trim() || 'root',
      roleIds: roleIds.length ? roleIds : undefined,
    };
    const file = this.files.length ? this.files[0] : undefined;

    this.api.updateDocumentViaForm(this.editingDoc.id, meta, file).subscribe({
      next: (ev: any) => {
        if (ev?.type === 1 && ev.total) this.progress = Math.round((ev.loaded * 100) / ev.total);
        if (ev?.type === 4) {
          this.uploading = false;
          this.modalRef?.hide();
          this.uploaded.emit();
        }
      },
      error: (e) => {
        this.uploading = false;
        this.submitError = e?.error?.message || 'Edit failed.';
      },
    });
  }
}
