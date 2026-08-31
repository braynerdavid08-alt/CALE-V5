import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { ApprenticeApi, ApprenticeDetail, ApprenticeDto } from '../api/apprentice.api';
import { TheoryApi } from '../../theory/api/theory.api';

@Component({
  selector: 'app-school-apprentices-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  templateUrl: './school-apprentices.page.html',
  styleUrl: './school-apprentices.page.css'
})
export class SchoolApprenticesPage implements OnInit {
  private readonly api = inject(ApprenticeApi);
  private readonly theoryApi = inject(TheoryApi);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly rows = signal<ApprenticeDto[]>([]);
  readonly selected = signal<ApprenticeDto | null>(null);
  readonly detail = signal<ApprenticeDetail | null>(null);
  readonly detailLoading = signal(false);
  readonly detailError = signal<string | null>(null);

  search = '';
  onlyBalance = false;

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.list(this.search || undefined, undefined, this.onlyBalance || undefined).subscribe({
      next: (rows) => {
        this.rows.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  select(row: ApprenticeDto): void {
    this.selected.set({ ...row });
    this.detail.set(null);
    this.detailError.set(null);
    this.detailLoading.set(true);
    this.api.getDetail(row.studentUserId).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.detailLoading.set(false);
      },
      error: (err) => {
        this.detailLoading.set(false);
        this.detailError.set(mapApiError(err));
      }
    });
  }

  save(): void {
    const row = this.selected();
    if (!row) return;
    this.saving.set(true);
    this.api.update(row.studentUserId, row).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.rows.update((list) => list.map((r) => (r.studentUserId === updated.studentUserId ? updated : r)));
        this.selected.set(updated);
        this.detail.update((d) => d ? { ...d, profile: updated } : d);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  canAuthorizeTheoryExam(): boolean {
    const d = this.detail();
    const row = this.selected();
    return !!d
      && d.training.theoryHoursComplete
      && d.training.workshopHoursComplete
      && !d.training.theoryExamPassed
      && (row?.balanceDue ?? 0) <= 0;
  }

  canAuthorizePractical(): boolean {
    const row = this.selected();
    return !!this.detail()?.training.theoryExamPassed
      && (row?.balanceDue ?? 0) <= 0;
  }

  hasBalanceDue(): boolean {
    return (this.selected()?.balanceDue ?? 0) > 0;
  }

  toggleTheoryExamAuth(authorized: boolean): void {
    const row = this.selected();
    if (!row) return;
    this.theoryApi.updateEnrollment(row.studentUserId, {
      status: row.enrollmentStatus,
      theoryExamAuthorized: authorized
    }).subscribe({
      next: () => this.select(row),
      error: (err) => this.detailError.set(mapApiError(err))
    });
  }

  togglePracticalAuth(authorized: boolean): void {
    const row = this.selected();
    if (!row) return;
    this.theoryApi.updateEnrollment(row.studentUserId, {
      status: row.enrollmentStatus,
      practicalAuthorized: authorized
    }).subscribe({
      next: () => this.select(row),
      error: (err) => this.detailError.set(mapApiError(err))
    });
  }

  formatMoney(value: number): string {
    return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(value);
  }
}
