// generate-report.modal.ts
import { Component, EventEmitter, Output, signal, effect, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

@Component({
  standalone: true,
  selector: 'app-generate-report-modal',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './generate-report.modal.html',
  styleUrls: ['./generate-report.modal.scss']
})
export class GenerateReportModal {
  @Output() submitReport = new EventEmitter<{
    type: string; period: string; start?: string; end?: string;
    includeCharts: boolean; includeData: boolean; includeSummary: boolean; includeRecommendations: boolean;
    format: string;
  }>();

  private fb = inject(FormBuilder);

  form = this.fb.group({
    type: ['attendance', Validators.required],
    period: ['last-month', Validators.required],
    start: [''],
    end: [''],
    includeCharts: [true],
    includeData: [true],
    includeSummary: [true],
    includeRecommendations: [false],
    format: ['html', Validators.required]
  });

  showCustomDates = signal(false);

  // runs after fields are initialized
  constructor() {
    effect(() => {
      this.showCustomDates.set(this.form.value.period === 'custom');
    });
  }

  onSubmit() {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.submitReport.emit({
      type: v.type!, period: v.period!,
      start: v.period === 'custom' ? v.start || undefined : undefined,
      end:   v.period === 'custom' ? v.end   || undefined : undefined,
      includeCharts: !!v.includeCharts, includeData: !!v.includeData,
      includeSummary: !!v.includeSummary, includeRecommendations: !!v.includeRecommendations,
      format: v.format!
    });
  }
}
