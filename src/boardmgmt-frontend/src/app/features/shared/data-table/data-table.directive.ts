import { AfterViewInit, Directive, ElementRef, Input, OnChanges, OnDestroy, Renderer2, SimpleChanges } from '@angular/core';

interface DataTableLabels {
  searchPlaceholder: string;
  perPage: string;
  info: string;
  noRows: string;
  previous: string;
  next: string;
}

interface DataTableConfig {
  perPage: number;
  perPageOptions: number[];
  searchable: boolean;
  sortable: boolean;
  pagination: boolean;
  labels: DataTableLabels;
}

type DataTableConfigInput = Partial<Omit<DataTableConfig, 'labels'>> & {
  labels?: Partial<DataTableLabels>;
};

const DEFAULT_CONFIG: DataTableConfig = {
  perPage: 10,
  perPageOptions: [10, 25, 50, 100],
  searchable: true,
  sortable: true,
  pagination: true,
  labels: {
    searchPlaceholder: 'Search…',
    perPage: 'Rows per page',
    info: 'Showing {start}–{end} of {total}',
    noRows: 'No records to display',
    previous: 'Previous',
    next: 'Next',
  },
};

@Directive({
  selector: 'table[appDataTable]',
  standalone: true,
})
export class DataTableDirective implements AfterViewInit, OnChanges, OnDestroy {
  @Input('appDataTable') configInput?: DataTableConfigInput | '';
  @Input() dataTableSource: unknown[] | null | undefined;

  private config: DataTableConfig = { ...DEFAULT_CONFIG };
  private readonly table: HTMLTableElement;
  private tbody?: HTMLTableSectionElement;
  private dataRows: HTMLTableRowElement[] = [];
  private placeholderRows: HTMLTableRowElement[] = [];
  private observer?: MutationObserver;
  private filterTerm = '';
  private currentPage = 1;
  private sortIndex: number | null = null;
  private sortDirection: 1 | -1 = 1;
  private listeners: Array<() => void> = [];

  private topToolbar?: HTMLElement;
  private bottomToolbar?: HTMLElement;
  private searchInput?: HTMLInputElement;
  private perPageSelect?: HTMLSelectElement;
  private infoLabel?: HTMLElement;
  private prevButton?: HTMLButtonElement;
  private nextButton?: HTMLButtonElement;

  constructor(private host: ElementRef<HTMLTableElement>, private renderer: Renderer2) {
    this.table = host.nativeElement;
  }

  ngAfterViewInit(): void {
    this.applyConfig();
    this.captureRows();
    this.setupControls();
    this.setupSorting();
    this.apply();
    this.observeMutations();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ('configInput' in changes && !changes['configInput'].firstChange) {
      this.applyConfig();
      this.syncPerPageSelect();
      this.apply();
    }

    if ('dataTableSource' in changes && !changes['dataTableSource'].firstChange) {
      queueMicrotask(() => {
        this.captureRows();
        this.apply();
      });
    }
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
    this.listeners.forEach(off => off());
    if (this.topToolbar) {
      const parent = this.renderer.parentNode(this.topToolbar);
      parent && this.renderer.removeChild(parent, this.topToolbar);
    }
    if (this.bottomToolbar) {
      const parent = this.renderer.parentNode(this.bottomToolbar);
      parent && this.renderer.removeChild(parent, this.bottomToolbar);
    }
  }

  private applyConfig() {
    const overrides: DataTableConfigInput =
      typeof this.configInput === 'object' && this.configInput ? this.configInput : {};
    const perPage = overrides.perPage ?? this.config.perPage;
    this.config = {
      ...DEFAULT_CONFIG,
      ...overrides,
      perPage,
      labels: {
        ...DEFAULT_CONFIG.labels,
        ...(overrides.labels ?? {}),
      },
    };
    if (!this.config.perPageOptions.includes(this.config.perPage) && this.config.perPage > 0) {
      this.config.perPageOptions = [...this.config.perPageOptions, this.config.perPage].sort((a, b) => a - b);
    }
  }

  private captureRows() {
    this.tbody = this.table.tBodies.item(0) ?? undefined;
    if (!this.tbody) {
      this.dataRows = [];
      this.placeholderRows = [];
      return;
    }
    const allRows = Array.from(this.tbody.rows);
    this.placeholderRows = allRows.filter(r => r.hasAttribute('data-dt-placeholder'));
    this.dataRows = allRows.filter(r => !r.hasAttribute('data-dt-placeholder'));
  }

  private setupControls() {
    const parent = this.renderer.parentNode(this.table);
    if (!parent || (this.topToolbar && this.bottomToolbar)) {
      return;
    }

    if (this.config.searchable || (this.config.pagination && this.config.perPageOptions.length)) {
      this.topToolbar = this.renderer.createElement('div');
      this.renderer.addClass(this.topToolbar, 'datatable-toolbar');
      this.renderer.addClass(this.topToolbar, 'd-flex');
      this.renderer.addClass(this.topToolbar, 'flex-wrap');
      this.renderer.addClass(this.topToolbar, 'gap-2');
      this.renderer.addClass(this.topToolbar, 'align-items-center');

      if (this.config.searchable) {
        const searchWrapper = this.renderer.createElement('div');
        this.renderer.addClass(searchWrapper, 'datatable-search');
        this.renderer.addClass(searchWrapper, 'input-group');
        this.renderer.addClass(searchWrapper, 'input-group-sm');

        const span = this.renderer.createElement('span');
        this.renderer.addClass(span, 'input-group-text');
        this.renderer.addClass(span, 'bg-light');
        this.renderer.addClass(span, 'border-end-0');
        span.innerHTML = '<i class="fas fa-search text-muted"></i>';

        this.searchInput = this.renderer.createElement('input');
        this.renderer.setAttribute(this.searchInput, 'type', 'search');
        this.renderer.addClass(this.searchInput, 'form-control');
        this.renderer.addClass(this.searchInput, 'border-start-0');
        this.renderer.setAttribute(this.searchInput, 'placeholder', this.config.labels.searchPlaceholder);

        const off = this.renderer.listen(this.searchInput, 'input', (event: Event) => {
          const value = (event.target as HTMLInputElement).value ?? '';
          this.filterTerm = value.trim().toLowerCase();
          this.currentPage = 1;
          this.apply();
        });
        this.listeners.push(off);

        this.renderer.appendChild(searchWrapper, span);
        this.renderer.appendChild(searchWrapper, this.searchInput);
        this.renderer.appendChild(this.topToolbar, searchWrapper);
      }

      if (this.config.pagination) {
        const perPageWrapper = this.renderer.createElement('div');
        this.renderer.addClass(perPageWrapper, 'datatable-per-page');
        this.renderer.addClass(perPageWrapper, 'd-flex');
        this.renderer.addClass(perPageWrapper, 'align-items-center');
        this.renderer.addClass(perPageWrapper, 'gap-2');

        const label = this.renderer.createElement('span');
        this.renderer.addClass(label, 'text-muted');
        this.renderer.addClass(label, 'small');
        label.textContent = this.config.labels.perPage;

        this.perPageSelect = this.renderer.createElement('select');
        this.renderer.addClass(this.perPageSelect, 'form-select');
        this.renderer.addClass(this.perPageSelect, 'form-select-sm');
        this.renderer.setStyle(this.perPageSelect, 'width', 'auto');

        const options = [...this.config.perPageOptions];
        if (!options.includes(0)) {
          options.push(0);
        }
        const normalized = Array.from(new Set(options)).sort((a, b) => (a === 0 ? Number.POSITIVE_INFINITY : a) - (b === 0 ? Number.POSITIVE_INFINITY : b));
        const select = this.perPageSelect;
        if (select) {
          normalized.forEach(opt => {
            const option = this.renderer.createElement('option');
            option.value = String(opt);
            option.textContent = opt === 0 ? 'All' : String(opt);
            this.renderer.appendChild(select, option);
          });
          select.value = String(this.config.perPage || 0);

          const off = this.renderer.listen(select, 'change', (event: Event) => {
            const value = Number((event.target as HTMLSelectElement).value);
            this.config.perPage = value === 0 ? 0 : Math.max(1, value);
            this.currentPage = 1;
            this.apply();
          });
          this.listeners.push(off);

          this.renderer.appendChild(perPageWrapper, label);
          this.renderer.appendChild(perPageWrapper, select);
        }
        this.renderer.appendChild(this.topToolbar, perPageWrapper);
      }

      this.renderer.insertBefore(parent, this.topToolbar, this.table);
    }

    if (this.config.pagination || this.config.searchable) {
      this.bottomToolbar = this.renderer.createElement('div');
      this.renderer.addClass(this.bottomToolbar, 'datatable-pagination');
      this.renderer.addClass(this.bottomToolbar, 'd-flex');
      this.renderer.addClass(this.bottomToolbar, 'flex-wrap');
      this.renderer.addClass(this.bottomToolbar, 'gap-2');
      this.renderer.addClass(this.bottomToolbar, 'align-items-center');
      this.renderer.addClass(this.bottomToolbar, 'justify-content-between');

      this.infoLabel = this.renderer.createElement('span');
      this.renderer.addClass(this.infoLabel, 'datatable-info');
      this.renderer.addClass(this.infoLabel, 'text-muted');
      this.renderer.addClass(this.infoLabel, 'small');
      this.renderer.appendChild(this.bottomToolbar, this.infoLabel);

      if (this.config.pagination) {
        const controls = this.renderer.createElement('div');
        this.renderer.addClass(controls, 'btn-group');
        this.renderer.addClass(controls, 'btn-group-sm');

        this.prevButton = this.renderer.createElement('button');
        const prevButton = this.prevButton;
        this.renderer.addClass(prevButton, 'btn');
        this.renderer.addClass(prevButton, 'btn-outline-secondary');
        prevButton.type = 'button';
        prevButton.innerHTML = `<i class="fas fa-chevron-left"></i> ${this.config.labels.previous}`;
        const prevOff = this.renderer.listen(prevButton, 'click', () => {
          if (this.currentPage > 1) {
            this.currentPage -= 1;
            this.apply();
          }
        });
        this.listeners.push(prevOff);

        this.nextButton = this.renderer.createElement('button');
        const nextButton = this.nextButton;
        this.renderer.addClass(nextButton, 'btn');
        this.renderer.addClass(nextButton, 'btn-outline-secondary');
        nextButton.type = 'button';
        nextButton.innerHTML = `${this.config.labels.next} <i class="fas fa-chevron-right"></i>`;
        const nextOff = this.renderer.listen(nextButton, 'click', () => {
          const totalPages = this.totalPages();
          if (this.currentPage < totalPages) {
            this.currentPage += 1;
            this.apply();
          }
        });
        this.listeners.push(nextOff);

        this.renderer.appendChild(controls, prevButton);
        this.renderer.appendChild(controls, nextButton);
        this.renderer.appendChild(this.bottomToolbar, controls);
      }

      this.renderer.appendChild(parent, this.bottomToolbar);
    }
  }

  private setupSorting() {
    if (!this.config.sortable || !this.table.tHead) {
      return;
    }
    const headerRow = this.table.tHead.rows.item(0);
    if (!headerRow) {
      return;
    }
    Array.from(headerRow.cells).forEach((cell, index) => {
      if (cell.classList.contains('no-sort') || cell.getAttribute('data-dt-sortable') === 'false') {
        return;
      }
      this.renderer.addClass(cell, 'datatable-sortable');
      const off = this.renderer.listen(cell, 'click', () => this.toggleSort(index));
      this.listeners.push(off);
    });
  }

  private toggleSort(index: number) {
    if (this.sortIndex === index) {
      this.sortDirection = this.sortDirection === 1 ? -1 : 1;
    } else {
      this.sortIndex = index;
      this.sortDirection = 1;
    }
    this.apply();
  }

  private apply() {
    if (!this.tbody) {
      return;
    }

    const rows = [...this.dataRows];
    const filtered = !this.filterTerm
      ? rows
      : rows.filter(r => (r.textContent ?? '').toLowerCase().includes(this.filterTerm));

    let processed = filtered;
    if (this.config.sortable && this.sortIndex !== null) {
      processed = [...processed].sort((a, b) => this.compareRows(a, b, this.sortIndex!) * this.sortDirection);
    }

    const totalRows = processed.length;
    const perPage = this.config.pagination ? this.config.perPage : 0;
    const totalPages = perPage > 0 ? Math.max(1, Math.ceil(totalRows / perPage)) : 1;
    if (this.currentPage > totalPages) {
      this.currentPage = totalPages;
    }

    const start = perPage > 0 ? (this.currentPage - 1) * perPage : 0;
    const end = perPage > 0 ? start + perPage : processed.length;
    const visible = perPage > 0 ? processed.slice(start, end) : processed;

    this.dataRows.forEach(row => this.renderer.setStyle(row, 'display', 'none'));
    visible.forEach(row => this.renderer.removeStyle(row, 'display'));

    if (this.placeholderRows.length) {
      const showPlaceholder = totalRows === 0;
      this.placeholderRows.forEach(row => {
        if (showPlaceholder) {
          this.renderer.removeStyle(row, 'display');
        } else {
          this.renderer.setStyle(row, 'display', 'none');
        }
      });
    }

    this.updateInfo(totalRows, visible.length ? start + 1 : 0, start + visible.length);
    this.updatePagerButtons(totalPages);
    this.updateSortIndicators();
  }

  private totalPages(): number {
    if (!this.config.pagination || this.config.perPage === 0) {
      return 1;
    }
    const perPage = this.config.perPage;
    const rows = !this.filterTerm
      ? this.dataRows.length
      : this.dataRows.filter(r => (r.textContent ?? '').toLowerCase().includes(this.filterTerm)).length;
    return Math.max(1, Math.ceil(rows / perPage));
  }

  private updateInfo(total: number, start: number, end: number) {
    if (!this.infoLabel) {
      return;
    }
    if (!total) {
      this.infoLabel.textContent = this.config.labels.noRows;
      return;
    }
    const text = this.config.labels.info
      .replace('{start}', String(start))
      .replace('{end}', String(end))
      .replace('{total}', String(total));
    this.infoLabel.textContent = text;
  }

  private updatePagerButtons(totalPages: number) {
    if (!this.prevButton || !this.nextButton) {
      return;
    }
    this.prevButton.disabled = this.currentPage <= 1;
    this.nextButton.disabled = this.currentPage >= totalPages || totalPages <= 1;
  }

  private updateSortIndicators() {
    if (!this.table.tHead) {
      return;
    }
    const headerRow = this.table.tHead.rows.item(0);
    if (!headerRow) {
      return;
    }
    Array.from(headerRow.cells).forEach((cell, index) => {
      this.renderer.removeClass(cell, 'datatable-sort-asc');
      this.renderer.removeClass(cell, 'datatable-sort-desc');
      if (this.sortIndex === index) {
        this.renderer.addClass(cell, this.sortDirection === 1 ? 'datatable-sort-asc' : 'datatable-sort-desc');
      }
    });
  }

  private compareRows(a: HTMLTableRowElement, b: HTMLTableRowElement, columnIndex: number): number {
    const cellA = a.cells.item(columnIndex);
    const cellB = b.cells.item(columnIndex);
    const textA = (cellA?.textContent ?? '').trim().toLowerCase();
    const textB = (cellB?.textContent ?? '').trim().toLowerCase();

    const numA = Number(textA.replace(/[^0-9.\-]+/g, ''));
    const numB = Number(textB.replace(/[^0-9.\-]+/g, ''));
    const bothNumbers = !Number.isNaN(numA) && !Number.isNaN(numB);
    if (bothNumbers) {
      return numA - numB;
    }
    return textA.localeCompare(textB);
  }

  private observeMutations() {
    if (!this.table.tBodies.length) {
      return;
    }
    this.observer = new MutationObserver(() => {
      this.captureRows();
      this.apply();
    });
    this.observer.observe(this.table.tBodies.item(0)!, { childList: true, subtree: true });
  }

  private syncPerPageSelect() {
    if (this.perPageSelect) {
      this.perPageSelect.value = String(this.config.perPage || 0);
    }
  }
}
