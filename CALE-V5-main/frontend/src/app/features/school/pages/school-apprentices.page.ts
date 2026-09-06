import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  ApprenticeApi,
  ApprenticeDetail,
  ApprenticeDto,
  EnrollmentAuthorizationEvent
} from '../api/apprentice.api';
import { EnrollmentDto, PracticalEligibilityDto, TheoryApi } from '../../theory/api/theory.api';
import { buildStudentBadges, SchoolBadge, StudentBadgeInput } from '../utils/school-student-badges';

@Component({
  selector: 'app-school-apprentices-page',
  standalone: true,
  imports: [
    DatePipe,
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
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly rows = signal<ApprenticeDto[]>([]);
  readonly progressByStudent = signal<Record<number, PracticalEligibilityDto>>({});
  readonly selected = signal<ApprenticeDto | null>(null);
  readonly detail = signal<ApprenticeDetail | null>(null);
  readonly detailLoading = signal(false);
  readonly detailError = signal<string | null>(null);

  search = '';
  onlyBalance = false;

  ngOnInit(): void {
    this.route.queryParamMap.subscribe((params) => {
      this.onlyBalance = params.get('withBalance') === 'true';
      this.reload();
    });
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      rows: this.api.list(this.search || undefined, undefined, this.onlyBalance || undefined),
      enrollments: this.theoryApi.listEnrollments().pipe(
        catchError((err) => {
          this.error.set(mapApiError(err));
          return of([] as EnrollmentDto[]);
        })
      )
    }).subscribe({
      next: ({ rows, enrollments }) => {
        this.rows.set(rows);
        this.progressByStudent.set(this.mapEnrollmentProgress(enrollments));
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private mapEnrollmentProgress(enrollments: EnrollmentDto[]): Record<number, PracticalEligibilityDto> {
    const map: Record<number, PracticalEligibilityDto> = {};
    for (const e of enrollments) {
      if (e.practicalEligibility) {
        map[e.studentUserId] = e.practicalEligibility;
      }
    }
    return map;
  }

  progressOf(row: ApprenticeDto): PracticalEligibilityDto | undefined {
    return this.progressByStudent()[row.studentUserId];
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

  formatHours(p?: PracticalEligibilityDto): string {
    if (!p) return '—';
    return `${p.theoryHoursCompleted}/${p.theoryHoursRequired}h`;
  }

  formatExamStatus(p?: PracticalEligibilityDto): string {
    if (!p) return '—';
    if (p.theoryExamPassed) return 'Aprobado';
    if (p.theoryExamAuthorized) return 'Autorizado';
    if (p.theoryHoursComplete && p.workshopHoursComplete) return 'Listo';
    return 'Pendiente';
  }

  formatPracticalStatus(row: ApprenticeDto, p?: PracticalEligibilityDto): string {
    if (row.practicalAuthorized || p?.canBookPractical) return 'Sí';
    if (p?.theoryExamPassed) return 'Examen OK';
    return 'No';
  }

  studentBadges(row: ApprenticeDto, training?: ApprenticeDetail['training'] | PracticalEligibilityDto): SchoolBadge[] {
    const input: StudentBadgeInput = {
      status: row.enrollmentStatus,
      balanceDue: row.balanceDue,
      theoryExamAuthorized: row.theoryExamAuthorized,
      practicalAuthorized: row.practicalAuthorized,
      isEnrolled: row.isEnrolled,
      runtRegistered: row.runtRegistered,
      theoryHoursComplete: training?.theoryHoursComplete,
      workshopHoursComplete: training?.workshopHoursComplete,
      theoryExamPassed: training?.theoryExamPassed
    };
    return buildStudentBadges(input);
  }

  authEventLabel(ev: EnrollmentAuthorizationEvent): string {
    const type = ev.authorizationType === 'theory_exam' ? 'Examen teórico' : 'Clases de manejo';
    const action = ev.action === 'granted' ? 'Autorizado' : 'Revocado';
    return `${type}: ${action}`;
  }
}
