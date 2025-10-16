import {
  Component,
  ElementRef,
  ViewChild,
  inject,
  Inject,
  PLATFORM_ID,
  AfterViewInit,
} from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import {
  VotingService,
  VoteDetailDto,
  VoteType,
  IndividualVoteDto,
} from '@core/services/voting.service';

@Component({
  standalone: true,
  selector: 'app-show-vote-modal',
  imports: [CommonModule],
  templateUrl: './show-vote.modal.html',
  styleUrls: ['./show-vote.modal.scss'],
})
export class ShowVoteModal implements AfterViewInit {
  private svc = inject(VotingService);
  constructor(@Inject(PLATFORM_ID) private platformId: Object) {}

  @ViewChild('resultsEl', { static: true }) resultsEl!: ElementRef<HTMLDivElement>;
  private modalRef: any;

  vote: VoteDetailDto | null = null;
  loading = false;
  error: string | null = null;

  VoteType = VoteType;

  async ngAfterViewInit() {
    if (!isPlatformBrowser(this.platformId)) return;

    // Ensure bootstrap.bundle is present (Modal lives on window.bootstrap)
    let bs = (window as any)?.bootstrap;
    if (!bs?.Modal) {
      await import('bootstrap/dist/js/bootstrap.bundle.min.js'); // types provided by our .d.ts
      bs = (window as any)?.bootstrap;
    }

    if (bs?.Modal && this.resultsEl?.nativeElement) {
      this.modalRef = bs.Modal.getOrCreateInstance(this.resultsEl.nativeElement, {
        backdrop: 'static',
      });
    }
  }

  private ensureModalReady(): boolean {
    const bs = (window as any)?.bootstrap;
    if (this.modalRef) return true;
    if (bs?.Modal && this.resultsEl?.nativeElement) {
      this.modalRef = bs.Modal.getOrCreateInstance(this.resultsEl.nativeElement, {
        backdrop: 'static',
      });
      return true;
    }
    return false;
  }

  open(voteId: string) {
    if (!this.ensureModalReady()) return; // no-op on SSR / before bundle

    this.loading = true;
    this.error = null;
    this.vote = null;

    this.svc.getOne(voteId).subscribe({
      next: (v) => {
        this.vote = v;
        this.loading = false;
        this.modalRef?.show?.();
      },
      error: () => {
        this.error = 'Failed to load vote.';
        this.loading = false;
        this.modalRef?.show?.();
      },
    });
  }

  close() {
    this.modalRef?.hide?.();
  }

  statusBadge() {
    if (!this.vote) return { text: '—', cls: 'bg-secondary' };
    if (this.vote.type === this.VoteType.MultipleChoice) return { text: 'Completed', cls: 'bg-info' };
    const { yes, no } = this.vote.results;
    if (yes > no) return { text: 'Approved', cls: 'bg-success' };
    if (no > yes) return { text: 'Rejected', cls: 'bg-danger' };
    return { text: 'Tie', cls: 'bg-secondary' };
  }

  exportResults() {
    if (!this.vote) return;
    const blob = new Blob([JSON.stringify(this.vote, null, 2)], { type: 'application/json' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `vote-${this.vote.id}-results.json`;
    a.click();
    URL.revokeObjectURL(a.href);
  }

  percent(part: number, total: number) {
    return !total ? 0 : Math.round((part / total) * 100);
  }

  labelFor(iv: IndividualVoteDto) {
    return iv.choiceLabel ?? iv.optionText ?? '—';
  }

  badgeFor(iv: IndividualVoteDto) {
    const label = (iv.choiceLabel ?? '').toLowerCase();
    if (label === 'yes' || label === 'approve') return 'bg-success';
    if (label === 'no' || label === 'reject') return 'bg-danger';
    if (label === 'abstain') return 'bg-secondary';
    return 'bg-info';
  }
}
