import {
  Component,
  Input,
  Output,
  EventEmitter,
  ViewChild,
  ElementRef,
  Inject,
  PLATFORM_ID,
  OnChanges,
  OnDestroy,
  SimpleChanges,
} from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { DocumentsService, DocumentDto } from '@core/services/documents.service';

declare global { interface Window { bootstrap?: any; } }

@Component({
  standalone: true,
  selector: 'app-document-viewer-modal',
  imports: [CommonModule],
  templateUrl: './document-viewer.modal.html',
  styleUrls: ['./document-viewer.modal.scss'],
})
export class DocumentViewerModalComponent implements OnChanges, OnDestroy {
  constructor(
    private sanitizer: DomSanitizer,
    private api: DocumentsService,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  @ViewChild('modalRoot', { static: false }) modalRoot?: ElementRef<HTMLDivElement>;

  private _doc: DocumentDto | null = null;
  @Input() set doc(value: DocumentDto | null) { this._doc = value; }
  get doc(): DocumentDto | null { return this._doc; }

  @Output() closed = new EventEmitter<void>();
  @Output() updated = new EventEmitter<void>();
  @Output() deleted = new EventEmitter<void>();
  @Output() editRequested = new EventEmitter<DocumentDto>();

  private pdfObjectUrl: string | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    // Auto-fetch a blob preview when doc changes and is a PDF
    if (this.doc && (this.doc.contentType || '').toLowerCase().includes('pdf')) {
      this.loadPdfBlob(this.doc.id);
    } else {
      // Not a PDF or doc cleared: release any previous object URL
      if (this.pdfObjectUrl) { URL.revokeObjectURL(this.pdfObjectUrl); this.pdfObjectUrl = null; }
    }
  }

  ngOnDestroy(): void {
    if (this.pdfObjectUrl) { URL.revokeObjectURL(this.pdfObjectUrl); this.pdfObjectUrl = null; }
  }

  private loadPdfBlob(id: string) {
    this.api.downloadBlob(id).subscribe({
      next: (blob: Blob) => {
        if (this.pdfObjectUrl) { URL.revokeObjectURL(this.pdfObjectUrl); this.pdfObjectUrl = null; }
        this.pdfObjectUrl = URL.createObjectURL(blob);
      },
      error: () => { /* graceful: previewUnavailable template shows download button */ }
    });
  }

  open() {
    if (!isPlatformBrowser(this.platformId)) return;
    const el = this.modalRoot?.nativeElement;
    if (!el) return;
    const Modal = (window as any).bootstrap?.Modal;
    if (!Modal) return;
    Modal.getOrCreateInstance(el).show();
  }

  close() {
    if (!isPlatformBrowser(this.platformId)) return;
    const el = this.modalRoot?.nativeElement;
    const Modal = (window as any).bootstrap?.Modal;
    if (el && Modal) Modal.getOrCreateInstance(el).hide();
    if (this.pdfObjectUrl) { URL.revokeObjectURL(this.pdfObjectUrl); this.pdfObjectUrl = null; }
    this.closed.emit();
  }

  // helpers
  typeFor(d: DocumentDto | null): 'pdf' | 'word' | 'excel' | 'powerpoint' | 'other' {
    if (!d) return 'other';
    const c = (d.contentType || '').toLowerCase();
    if (c.includes('pdf')) return 'pdf';
    if (c.includes('presentation') || c.includes('powerpoint') || c.includes('ppt')) return 'powerpoint';
    if (c.includes('spreadsheet') || c.includes('excel') || c.includes('sheet')) return 'excel';
    if (c.includes('word') || c.includes('msword') || c.includes('officedocument.wordprocessingml')) return 'word';
    return 'other';
  }

  faIconFor(d: DocumentDto | null): string {
    switch (this.typeFor(d)) {
      case 'pdf': return 'fa-file-pdf';
      case 'word': return 'fa-file-word';
      case 'excel': return 'fa-file-excel';
      case 'powerpoint': return 'fa-file-powerpoint';
      default: return 'fa-file';
    }
  }

  prettySize(bytes?: number): string {
    const b = bytes ?? 0;
    if (b < 1024) return `${b} B`;
    const kb = b / 1024; if (kb < 1024) return `${kb.toFixed(1)} KB`;
    const mb = kb / 1024; if (mb < 1024) return `${mb.toFixed(1)} MB`;
    const gb = mb / 1024; return `${gb.toFixed(1)} GB`;
  }

  /** Use the blob URL if available (preferred for PDFs). */
  safePdfUrl(d: DocumentDto | null): SafeResourceUrl | null {
    if (!d || !this.pdfObjectUrl) return null;
    return this.sanitizer.bypassSecurityTrustResourceUrl(this.pdfObjectUrl);
    // If you want to fall back to d.url when same-origin and allowed, you can add that here.
  }

  // actions
  beginEdit() { if (this.doc) this.editRequested.emit(this.doc); }

  confirmDelete() {
    if (!this.doc) return;
    const ok = confirm(`Delete "${this.doc.originalName}"? This cannot be undone.`);
    if (!ok) return;
    this.api.deleteDocument(this.doc.id).subscribe({
      next: () => { this.deleted.emit(); this.close(); },
      error: () => alert('Failed to delete document.'),
    });
  }

  downloadDocument(d: DocumentDto | null) {
    if (!d) return;
    this.api.downloadBlob(d.id).subscribe({
      next: (blob: Blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = d.originalName || 'download';
        document.body.appendChild(a); a.click(); a.remove();
        URL.revokeObjectURL(url);
      },
      error: () => alert('Failed to download document.')
    });
  }

  async shareDocument(d: DocumentDto | null) {
    if (!d || !isPlatformBrowser(this.platformId)) return;
    const shareData = { title: d.originalName, text: d.description || d.originalName, url: d.url };
    try {
      if (navigator.share) await navigator.share(shareData);
      else if (navigator.clipboard) { await navigator.clipboard.writeText(d.url); alert('Link copied'); }
      else prompt('Copy link:', d.url);
    } catch {}
  }
}
