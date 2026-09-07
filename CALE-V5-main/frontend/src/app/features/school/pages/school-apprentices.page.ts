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
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  ApprenticeApi,
  ApprenticeDetail,
  ApprenticeDto,
  EnrollmentAuthorizationEvent,
  PracticalEligibility
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
    UiPageHeaderComponent,
    UiSuccessComponent
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
  readonly saveOk = signal<string | null>(null);
  readonly rows = signal<ApprenticeDto[]>([]);
  readonly progressByStudent = signal<Record<number, PracticalEligibilityDto>>({});
  readonly selected = signal<ApprenticeDto | null>(null);
  readonly detail = signal<ApprenticeDetail | null>(null);
  readonly detailLoading = signal(false);
  readonly detailError = signal<string | null>(null);

  search = '';
  onlyBalance = false;

  readonly licenseCategoryOptions = [
    { value: 'A2', label: 'A2' },
    { value: 'B1', label: 'B1' },
    { value: 'C1', label: 'C1' },
    { value: 'A2,B1', label: 'A2 + B1' },
    { value: 'A2,C1', label: 'A2 + C1' }
  ];

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

  /** Progress from detail, or from the enrollments list while detail loads/fails. */
  trainingOf(row?: ApprenticeDto | null): PracticalEligibilityDto | PracticalEligibility | undefined {
    const selected = row ?? this.selected();
    if (!selected) return undefined;
    const d = this.detail();
    if (d && d.profile.studentUserId === selected.studentUserId) {
      return d.training;
    }
    return this.progressOf(selected);
  }

  select(row: ApprenticeDto): void {
    this.selected.set({ ...row });
    this.detail.set(null);
    this.detailError.set(null);
    this.detailLoading.set(true);
    this.api.getDetail(row.studentUserId).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.selected.set({ ...detail.profile });
        this.rows.update((list) =>
          list.map((r) =>
            r.studentUserId === detail.profile.studentUserId ? detail.profile : r
          )
        );
        this.progressByStudent.update((map) => ({
          ...map,
          [detail.profile.studentUserId]: detail.training
        }));
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
    this.error.set(null);
    this.saveOk.set(null);
    const body = {
      documentType: row.documentType,
      documentNumber: row.documentNumber,
      phone: row.phone,
      address: row.address,
      contactEmail: row.contactEmail,
      enrollmentMonth: row.enrollmentMonth,
      enrollmentDate: row.enrollmentDate,
      orderNumber: row.orderNumber,
      licenseCategories: row.licenseCategories,
      attendanceDayType: row.attendanceDayType,
      scheduleSlot: row.scheduleSlot,
      receiptNumber: row.receiptNumber,
      amountDue: Number(row.amountDue) || 0,
      amountPaid: Number(row.amountPaid) || 0,
      paymentMethod: row.paymentMethod,
      balancePaymentAmount: row.balancePaymentAmount,
      accountsReceivable: Number(row.accountsReceivable) || 0,
      balancePaymentDate: row.balancePaymentDate,
      balancePaymentMethod: row.balancePaymentMethod,
      balanceReceiptNumber: row.balanceReceiptNumber,
      enrollmentPin: row.enrollmentPin,
      runtRegistered: !!row.runtRegistered,
      isEnrolled: !!row.isEnrolled,
      notes: row.notes
    };
    this.api.update(row.studentUserId, body).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.rows.update((list) => list.map((r) => (r.studentUserId === updated.studentUserId ? updated : r)));
        this.selected.set(updated);
        this.detail.update((d) => d ? { ...d, profile: updated } : d);
        this.saveOk.set(
          `Pagos guardados: pagado ${this.formatMoney(updated.amountPaid)}, saldo ${this.formatMoney(updated.balanceDue)}.`
        );
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  canAuthorizeTheoryExam(): boolean {
    const t = this.trainingOf();
    const row = this.selected();
    return !!t
      && t.theoryHoursComplete
      && t.workshopHoursComplete
      && !t.theoryExamPassed
      && (row?.balanceDue ?? 0) <= 0;
  }

  canAuthorizePractical(): boolean {
    const row = this.selected();
    return !!this.trainingOf()?.theoryExamPassed
      && (row?.balanceDue ?? 0) <= 0;
  }

  theoryAuthHint(): string {
    const row = this.selected();
    if (!row) return '';
    if (row.theoryExamAuthorized) {
      return 'Autorizado para agendar cita de examen.';
    }
    if (this.detailLoading() && !this.trainingOf(row)) {
      return 'Cargando progreso…';
    }
    if (this.detailError() && !this.trainingOf(row)) {
      return `Progreso no disponible (${this.detailError()}). Aun así puedes autorizar manualmente.`;
    }
    const t = this.trainingOf(row);
    if (!t) {
      return 'Sin datos de progreso. Puedes autorizar manualmente si corresponde.';
    }
    if (t.theoryExamPassed) {
      return 'Ya aprobó el examen teórico.';
    }
    if (this.canAuthorizeTheoryExam()) {
      return 'Completó teoría y taller — listo para autorizar.';
    }
    const balance = this.hasBalanceDue() ? ' Tiene saldo pendiente.' : '';
    return `Contador: ${t.theoryHoursCompleted}/${t.theoryHoursRequired} h teoría, ${t.workshopHoursCompleted}/${t.workshopHoursRequired} h taller.${balance} Puedes autorizar igual si la escuela lo confirma.`;
  }

  practicalAuthHint(): string {
    const row = this.selected();
    if (!row) return '';
    if (row.practicalAuthorized) {
      return 'Autorizado para programar y reservar práctica.';
    }
    if (this.detailLoading() && !this.trainingOf(row)) {
      return 'Cargando progreso…';
    }
    if (this.canAuthorizePractical()) {
      return 'Aprobó el examen — listo para autorizar manejo.';
    }
    if (this.trainingOf()?.theoryExamPassed) {
      return this.hasBalanceDue()
        ? 'Saldo pendiente. Puedes autorizar manejo manualmente si corresponde.'
        : 'Listo según examen; confirma autorización de manejo.';
    }
    return 'Aún no figura examen aprobado. Puedes autorizar manejo manualmente si la escuela lo confirma.';
  }

  hasBalanceDue(): boolean {
    return (this.selected()?.balanceDue ?? 0) > 0;
  }

  private confirmManualOverride(kind: 'examen' | 'manejo'): boolean {
    if (kind === 'examen' && this.canAuthorizeTheoryExam()) {
      return true;
    }
    if (kind === 'manejo' && this.canAuthorizePractical()) {
      return true;
    }
    return confirm(
      kind === 'examen'
        ? 'El aprendiz no cumple todos los requisitos automáticos (horas/saldo). ¿Autorizar examen teórico de todas formas?'
        : 'El aprendiz no cumple todos los requisitos automáticos (examen/saldo). ¿Autorizar clases de manejo de todas formas?'
    );
  }

  toggleTheoryExamAuth(authorized: boolean): void {
    const row = this.selected();
    if (!row) return;
    if (authorized && !this.confirmManualOverride('examen')) {
      return;
    }
    this.detailError.set(null);
    this.theoryApi.updateEnrollment(row.studentUserId, {
      status: row.enrollmentStatus,
      theoryExamAuthorized: authorized
    }).subscribe({
      next: (enrollment) => this.applyEnrollmentPatch(row, enrollment),
      error: (err) => this.detailError.set(mapApiError(err))
    });
  }

  togglePracticalAuth(authorized: boolean): void {
    const row = this.selected();
    if (!row) return;
    if (authorized && !this.confirmManualOverride('manejo')) {
      return;
    }
    this.detailError.set(null);
    this.theoryApi.updateEnrollment(row.studentUserId, {
      status: row.enrollmentStatus,
      practicalAuthorized: authorized
    }).subscribe({
      next: (enrollment) => this.applyEnrollmentPatch(row, enrollment),
      error: (err) => this.detailError.set(mapApiError(err))
    });
  }

  private applyEnrollmentPatch(row: ApprenticeDto, enrollment: EnrollmentDto): void {
    const patched: ApprenticeDto = {
      ...row,
      enrollmentStatus: enrollment.status,
      theoryExamAuthorized: enrollment.theoryExamAuthorized,
      practicalAuthorized: enrollment.practicalAuthorized,
      attendanceDayType: enrollment.attendanceDayType ?? row.attendanceDayType,
      licenseCategories: enrollment.licenseCategories ?? row.licenseCategories
    };
    this.rows.update((list) =>
      list.map((r) => (r.studentUserId === patched.studentUserId ? patched : r))
    );
    if (enrollment.practicalEligibility) {
      this.progressByStudent.update((map) => ({
        ...map,
        [patched.studentUserId]: enrollment.practicalEligibility!
      }));
    }
    this.select(patched);
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
