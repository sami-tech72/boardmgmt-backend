import { Component, ViewChild, OnInit, Inject, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  VotingService,
  VoteSummaryDto,
  VoteType,
  VoteChoice,
} from '@core/services/voting.service';
import { CreateVoteModal } from '../shared/create-vote-modal/create-vote.modal';
import { ShowVoteModal } from '../shared/show-vote-modal/show-vote.modal';
import { UserMenuComponent } from '../shared/user-menu/user-menu.component';
import { PageHeaderComponent } from '../shared/page-header/page-header.component';

interface VoteTallies {
  totalBallots: number;
  yes: number;
  no: number;
  abstain: number;
  options?: Array<{ id: string; text: string; count: number }>;
}

@Component({
  standalone: true,
  selector: 'app-voting',
  imports: [CommonModule, FormsModule, CreateVoteModal, ShowVoteModal, UserMenuComponent, PageHeaderComponent],
  templateUrl: './voting.page.html',
  styleUrls: ['./voting.page.scss'],
})
export class VotingPage implements OnInit {
  constructor(
    private voting: VotingService,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  @ViewChild('voteModal')    voteModal!: CreateVoteModal;
  @ViewChild('resultsModal') resultsModal!: ShowVoteModal;

  openVoteModal() { this.voteModal?.open(); }
  openResults(id: string) { this.resultsModal?.open(id); }

  VoteType = VoteType;
  VoteChoice = VoteChoice;
  Math: typeof Math = Math;

  active: VoteSummaryDto[] = [];
  recent: VoteSummaryDto[] = [];

  selection:  Record<string, string> = {};
  submitting: Record<string, boolean> = {};

  private static readonly EMPTY_RESULTS: VoteTallies = {
    totalBallots: 0, yes: 0, no: 0, abstain: 0, options: [],
  };

  res(v: VoteSummaryDto): VoteTallies {
    return (v as any).results ?? VotingPage.EMPTY_RESULTS;
  }

  private colorOf(v: VoteSummaryDto): 'success' | 'warning' | 'info' {
    switch (v.type) {
      case VoteType.YesNo:          return 'success';
      case VoteType.ApproveReject:  return 'warning';
      case VoteType.MultipleChoice: return 'info';
      default:                      return 'warning';
    }
  }
  themeClass(v: VoteSummaryDto)  { return this.colorOf(v) === 'warning' ? 'theme-warning' : 'theme-info'; }
  borderClass(v: VoteSummaryDto) { return `border-${this.colorOf(v)}`; }
  headerClass(v: VoteSummaryDto) { const c = this.colorOf(v); return c === 'warning' ? 'bg-warning text-dark' : 'bg-info text-white'; }
  buttonClass(v: VoteSummaryDto) { return `btn-${this.colorOf(v)}`; }
  progressBarClass(v: VoteSummaryDto) { return `bg-${this.colorOf(v)}`; }
  trackById = (_: number, v: VoteSummaryDto) => v.id;

  ngOnInit() {
    if (isPlatformBrowser(this.platformId)) {
      this.refresh();
    }
  }

  refresh() {
    this.voting.listActive().subscribe({
      next: (a) => {
        this.active = a ?? [];
        this.hydrateSelections(this.active);
      },
      error: () => { this.active = []; }
    });

    this.voting.listRecent().subscribe({
      next: (r) => { this.recent = r ?? []; },
      error: () => { this.recent = []; }
    });
  }

  private hydrateSelections(list: VoteSummaryDto[]) {
    for (const v of list) {
      if (!this.selection[v.id]) {
        if (v.type === VoteType.MultipleChoice && (v as any).myOptionId) {
          this.selection[v.id] = (v as any).myOptionId;
        } else if ((v as any).myChoice != null) {
          const c = (v as any).myChoice as VoteChoice;
          this.selection[v.id] =
            c === VoteChoice.Yes ? 'yes' : c === VoteChoice.No ? 'no' : 'abstain';
        }
      }
    }
  }

  endsIn(deadlineIso: string) {
    const end = new Date(deadlineIso).getTime();
    const diff = Math.max(0, end - Date.now());
    const d = Math.floor(diff / (24 * 3600 * 1000));
    const h = Math.floor((diff % (24 * 3600 * 1000)) / (3600 * 1000));
    if (d > 0) return `Ends in ${d} day${d > 1 ? 's' : ''}`;
    return `Ends in ${h} hour${h !== 1 ? 's' : ''}`;
  }

  percent(n: number, d: number) { return !d ? 0 : Math.round((n / d) * 100); }

  setChoice(v: VoteSummaryDto, value: string) {
    if (this.submitting[v.id]) return;
    this.selection[v.id] = value;
  }

  isVoted(v: VoteSummaryDto) { return !!(v as any).alreadyVoted; }

  submitLabel(v: VoteSummaryDto) {
    if (this.submitting[v.id]) return 'Submitting...';
    return this.isVoted(v) ? 'Update Vote' : 'Submit Vote';
  }

  private applyUpdatedSummary(updated: VoteSummaryDto) {
    const i = this.active.findIndex(x => x.id === updated.id);
    if (i >= 0) this.active[i] = updated;

    if (updated.type === VoteType.MultipleChoice && (updated as any).myOptionId) {
      this.selection[updated.id] = (updated as any).myOptionId;
    } else if ((updated as any).myChoice != null) {
      const c = (updated as any).myChoice as VoteChoice;
      this.selection[updated.id] =
        c === VoteChoice.Yes ? 'yes' : c === VoteChoice.No ? 'no' : 'abstain';
    }

    this.submitting[updated.id] = false;
  }

  submit(v: VoteSummaryDto) {
    const val = this.selection[v.id];
    if (!val || this.submitting[v.id]) return;

    this.submitting[v.id] = true;

    const next  = (res: VoteSummaryDto) => this.applyUpdatedSummary(res);
    const error = () => { this.submitting[v.id] = false; };

    if (v.type === VoteType.MultipleChoice) {
      this.voting.submitBallot(v.id, { optionId: val, choice: null })
        .subscribe({ next, error });
    } else {
      const choice =
        v.type === VoteType.YesNo
          ? (val === 'yes' ? VoteChoice.Yes : val === 'no' ? VoteChoice.No : VoteChoice.Abstain)
          : (val === 'approve' ? VoteChoice.Yes : val === 'reject' ? VoteChoice.No : VoteChoice.Abstain);

      this.voting.submitBallot(v.id, { choice, optionId: null })
        .subscribe({ next, error });
    }
  }

  get activeCount() { return this.active.length; }
  get completedCount() { return this.recent.length; }
  get pendingCount() {
    return Math.max(0, this.active.length - this.active.filter(a => this.res(a).totalBallots > 0).length);
  }
  get participationRate() {
    const totals = this.active.map(a => this.res(a).totalBallots);
    if (!totals.length) return 0;
    const max = Math.max(...totals, 1);
    const avg = totals.reduce((s, x) => s + x, 0) / totals.length;
    return Math.min(100, Math.round((avg / max) * 100)) || 0;
  }

  onLogout(): void {
    // optional: extra cleanup or message
    console.log('User logged out from dashboard');
    // No need to navigate manually — UserMenuComponent already does that.
  }
}
