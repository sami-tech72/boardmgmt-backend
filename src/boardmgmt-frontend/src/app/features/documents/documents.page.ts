import { Component, ViewChild, signal, computed, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser, CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UploadDocumentModal } from '../shared/upload-document-modal/upload-document.modal';
import { NewFolderModal } from '../shared/new-folder-modal/new-folder.modal';
import { DocumentViewerModalComponent } from '../shared/document-viewer-modal/document-viewer.modal';
import { DataTableDirective } from '../shared/data-table/data-table.directive';
import {
  DocumentsService,
  FolderDto,
  DocumentDto,
  DocType,
  DatePreset,
} from '@core/services/documents.service';
import { UserMenuComponent } from '../shared/user-menu/user-menu.component';

declare global {
  interface Window {
    bootstrap?: any;
  }
}

type FolderStyle = { colorClass: string; iconClass: string };

@Component({
  standalone: true,
  selector: 'app-documents',
  imports: [
    CommonModule,
    FormsModule,
    UploadDocumentModal,
    NewFolderModal,
    DocumentViewerModalComponent,
    UserMenuComponent,
    DataTableDirective,
  ],
  templateUrl: './documents.page.html',
  styleUrls: ['./documents.page.scss'],
})
export class DocumentsPage {
  constructor(private api: DocumentsService, @Inject(PLATFORM_ID) private platformId: Object) {}

  foldersLoaded = false;

  @ViewChild(NewFolderModal) newFolderModal!: NewFolderModal;
  @ViewChild(UploadDocumentModal) uploadModal!: UploadDocumentModal;
  @ViewChild(DocumentViewerModalComponent) viewer!: DocumentViewerModalComponent;

  folders: FolderDto[] = [];
  docs: DocumentDto[] = [];

  viewMode = signal<'grid' | 'list'>('grid');
  folderSlug = signal<string>('');
  type = signal<DocType | ''>('');
  datePreset = signal<DatePreset | ''>('');
  search = signal<string>('');
  selectedDoc = signal<DocumentDto | null>(null);

  private readonly FOLDER_PALETTE: FolderStyle[] = [
    { colorClass: 'text-primary', iconClass: 'fas fa-folder' },
    { colorClass: 'text-success', iconClass: 'fas fa-folder' },
    { colorClass: 'text-warning', iconClass: 'fas fa-folder' },
    { colorClass: 'text-info', iconClass: 'fas fa-folder' },
    { colorClass: 'text-danger', iconClass: 'fas fa-folder' },
    { colorClass: 'text-secondary', iconClass: 'fas fa-folder' },
  ];
  private styleMap: Record<string, FolderStyle> = {};

  currentFolderName = computed(() => {
    const slug = this.folderSlug();
    if (!slug) return 'All Documents';
    const f = this.folders.find((x) => x.slug === slug);
    return f?.name ?? 'All Documents';
  });

  get existingFolderSlugs(): string[] {
    return this.folders.map((f) => f.slug);
  }

  ngOnInit() {
    // ❗️No HTTP calls during SSR.
    if (!isPlatformBrowser(this.platformId)) return;
    this.refreshFolders();
    this.refreshDocs();
  }

  refreshFolders() {
    this.api.listFolders().subscribe((f) => {
      this.folders = f ?? [];
      this.assignFolderStyles();
      this.foldersLoaded = true;
    });
  }

  private assignFolderStyles() {
    const map: Record<string, FolderStyle> = {};
    this.folders.forEach((folder, idx) => {
      const style = this.FOLDER_PALETTE[idx % this.FOLDER_PALETTE.length];
      if (folder?.slug) map[folder.slug.toLowerCase()] = style;
    });
    this.styleMap = map;
  }

  styleForFolder = (f: FolderDto): FolderStyle => {
    const slug = (f.slug || '').toLowerCase();
    return this.styleMap[slug] ?? { colorClass: 'text-secondary', iconClass: 'fas fa-folder' };
  };

  refreshDocs() {
    if (!isPlatformBrowser(this.platformId)) return; // guard
    const t = this.type();
    const dp = this.datePreset();
    const fs = (this.folderSlug() ?? '').trim();
    this.api
      .listDocuments({
        folderSlug: fs.length ? fs : undefined,
        type: t === '' ? undefined : t,
        search: this.search() || undefined,
        datePreset: dp === '' ? undefined : dp,
      })
      .subscribe((d) => (this.docs = d ?? []));
  }

  // Filters + actions
  createFolder() {
    this.newFolderModal?.open();
  }
  onFolderCreate = (name: string) => {
    this.api.createFolder(name).subscribe(() => this.refreshFolders());
  };
  openUpload() {
    this.uploadModal?.openForCreate();
  }
  onFiltersChanged() {
    this.refreshDocs();
  }

  // Icons
  typeFor(d: DocumentDto): 'pdf' | 'word' | 'excel' | 'powerpoint' | 'other' {
    const c = (d.contentType || '').toLowerCase();
    if (c.includes('pdf')) return 'pdf';
    if (c.includes('presentation') || c.includes('powerpoint') || c.includes('ppt'))
      return 'powerpoint';
    if (c.includes('spreadsheet') || c.includes('excel') || c.includes('sheet')) return 'excel';
    if (c.includes('word') || c.includes('msword') || c.includes('officedocument.wordprocessingml'))
      return 'word';
    return 'other';
  }
  faIconFor(d: DocumentDto | null): string {
    if (!d) return 'fa-file fa-2x';
    switch (this.typeFor(d)) {
      case 'pdf':
        return 'fa-file-pdf fa-2x';
      case 'word':
        return 'fa-file-word fa-2x';
      case 'excel':
        return 'fa-file-excel fa-2x';
      case 'powerpoint':
        return 'fa-file-powerpoint fa-2x';
      default:
        return 'fa-file fa-2x';
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

  /** Fetch by id then open viewer */
  viewDocument(d: DocumentDto) {
    this.api.getDocumentById(d.id).subscribe({
      next: (full) => {
        this.selectedDoc.set(full);
        this.viewer.open();
      },
      error: () => alert('You don’t have access to this document or it no longer exists.'),
    });
  }

  onViewerClosed() {
    this.selectedDoc.set(null);
  }

  onEditRequested(d: DocumentDto) {
    this.viewer.close();
    setTimeout(() => this.uploadModal.openForEdit(d));
  }

  downloadDocument(d: DocumentDto) {
    this.api.downloadBlob(d.id).subscribe({
      next: (blob: Blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = d.originalName || 'download';
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
      },
      error: () => alert('Failed to download document.'),
    });
  }

  async shareDocument(d: DocumentDto) {
    if (!isPlatformBrowser(this.platformId)) return;
    const shareData = { title: d.originalName, text: d.description || d.originalName, url: d.url };
    try {
      if (navigator.share) await navigator.share(shareData);
      else if (navigator.clipboard) {
        await navigator.clipboard.writeText(d.url);
        alert('Link copied');
      } else prompt('Copy link:', d.url);
    } catch {}
  }

  onLogout(): void {
    // optional: extra cleanup or message
    console.log('User logged out from dashboard');
    // No need to navigate manually — UserMenuComponent already does that.
  }
}
