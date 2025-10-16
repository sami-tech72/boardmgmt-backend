import {
  Component, EventEmitter, Output, ViewChild, ElementRef, Input,
  Inject, PLATFORM_ID, AfterViewInit
} from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';

declare global {
  interface Window { bootstrap?: any; }
}

@Component({
  standalone: true,
  selector: 'app-new-folder-modal',
  imports: [CommonModule, FormsModule],
  templateUrl: './new-folder.modal.html',
  styleUrls: ['./new-folder.modal.scss'],
})
export class NewFolderModal implements AfterViewInit {
  constructor(@Inject(PLATFORM_ID) private platformId: Object) {}

  @Input() existingSlugs: string[] = [];
  @Output() create = new EventEmitter<string>();

  @ViewChild('modalEl', { static: true }) modalEl!: ElementRef<HTMLDivElement>;
  @ViewChild('nameInput') nameInput!: ElementRef<HTMLInputElement>;

  name = '';
  error = '';
  private modalRef: any | null = null;

  ngAfterViewInit(): void {
    // init only in the browser and only if bootstrap is on window
    if (!isPlatformBrowser(this.platformId)) return;
    const Modal = (window as any).bootstrap?.Modal;
    if (Modal) this.modalRef = new Modal(this.modalEl.nativeElement, { backdrop: 'static' });
  }

  private ensureModal() {
    if (!isPlatformBrowser(this.platformId)) return;
    if (!this.modalRef) {
      const Modal = (window as any).bootstrap?.Modal;
      if (Modal) this.modalRef = new Modal(this.modalEl.nativeElement, { backdrop: 'static' });
    }
  }

  open() {
    this.name = '';
    this.error = '';
    this.ensureModal();
    this.modalRef?.show();
    setTimeout(() => this.nameInput?.nativeElement?.focus(), 200);
  }

  close() { this.modalRef?.hide(); }

  submit() {
    const value = (this.name ?? '').trim();
    if (!value) { this.error = 'Please enter a folder name.'; return; }
    if (value.length > 60) { this.error = 'Folder name cannot exceed 60 characters.'; return; }
    const slug = this.slugify(value);
    if (this.existingSlugs.includes(slug)) { this.error = 'A folder with this name already exists.'; return; }
    this.error = '';
    this.create.emit(value);
    this.close();
  }

  slugify(s: string) {
    return s.toLowerCase().trim()
      .replace(/[^\w\s-]/g, '')
      .replace(/\s+/g, '-')
      .replace(/-+/g, '-');
  }
}
