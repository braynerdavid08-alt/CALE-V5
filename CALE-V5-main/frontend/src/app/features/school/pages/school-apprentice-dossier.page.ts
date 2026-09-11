import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import {
  ApprenticeApi,
  ApprenticeDetail,
  EnrollmentAuthorizationEvent
} from '../api/apprentice.api';

type StepState = 'done' | 'active' | 'todo' | 'blocked';

interface PipelineStep {
  id: string;
  label: string;
  detail: string;
  state: StepState;
}

interface TimelineItem {
  at: string;
  title: string;
  detail: string;
  tone: 'ok' | 'warn' | 'info' | 'neutral';
}

@Component({
  selector: 'app-school-apprentice-dossier-page',
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
  templateUrl: './school-apprentice-dossier.page.html',
  styleUrl: './school-apprentice-dossier.page.css'
})
export class SchoolApprenticeDossierPage implements OnInit {
  private readonly api = inject(ApprenticeApi);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly detail = signal<ApprenticeDetail | null>(null);
  readonly savingAbono = signal(false);
  readonly abonoError = signal<string | null>(null);
  readonly abonoOk = signal<string | null>(null);
  studentUserId = 0;

  abonoDate = '';
  abonoAmount: number | null = null;
  abonoMethod = '';
  abonoReceipt = '';
  abonoNotes = '';

  readonly pipeline = computed(() => this.buildPipeline(this.detail()));
  readonly timeline = computed(() => this.buildTimeline(this.detail()));

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      this.studentUserId = Number(params.get('studentUserId') || 0);
      if (this.studentUserId > 0) {
        this.resetAbonoForm();
        this.reload();
      } else {
        this.loading.set(false);
        this.error.set('Falta el aprendiz a consultar.');
      }
    });
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getDetail(this.studentUserId).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  registerAbono(): void {
    const amount = Number(this.abonoAmount);
    if (!this.abonoDate || !(amount > 0)) {
      this.abonoError.set('Indica fecha y un monto mayor a cero.');
      this.abonoOk.set(null);
      return;
    }

    this.savingAbono.set(true);
    this.abonoError.set(null);
    this.abonoOk.set(null);
    this.api
      .registerAbono(this.studentUserId, {
        paymentDate: this.abonoDate,
        amount,
        paymentMethod: this.abonoMethod || null,
        receiptNumber: this.abonoReceipt || null,
        notes: this.abonoNotes || null
      })
      .subscribe({
        next: (cartera) => {
          const current = this.detail();
          if (current) {
            this.detail.set({
              ...current,
              cartera,
              profile: {
                ...current.profile,
                amountDue: cartera.amountDue,
                amountPaid: cartera.amountPaid,
                balanceDue: cartera.balanceDue,
                accountsReceivable: cartera.accountsReceivable,
                paymentMethod: cartera.paymentMethod ?? current.profile.paymentMethod,
                receiptNumber: cartera.receiptNumber ?? current.profile.receiptNumber,
                balancePaymentAmount: amount,
                balancePaymentDate: this.abonoDate,
                balancePaymentMethod: this.abonoMethod || current.profile.balancePaymentMethod,
                balanceReceiptNumber: this.abonoReceipt || current.profile.balanceReceiptNumber
              }
            });
          }
          this.savingAbono.set(false);
          this.abonoOk.set('Abono registrado.');
          this.resetAbonoForm();
        },
        error: (err) => {
          this.savingAbono.set(false);
          this.abonoError.set(mapApiError(err));
        }
      });
  }

  formatMoney(value: number | null | undefined): string {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      maximumFractionDigits: 0
    }).format(Number(value) || 0);
  }

  authEventLabel(ev: EnrollmentAuthorizationEvent): string {
    const type =
      ev.authorizationType === 'TheoryExam'
        ? 'Examen teórico'
        : ev.authorizationType === 'Practical'
          ? 'Clases de manejo'
          : ev.authorizationType;
    const action =
      ev.action === 'Granted'
        ? 'autorizado'
        : ev.action === 'Revoked'
          ? 'revocado'
          : ev.action;
    return `${type}: ${action}`;
  }

  private resetAbonoForm(): void {
    const today = new Date();
    const yyyy = today.getFullYear();
    const mm = String(today.getMonth() + 1).padStart(2, '0');
    const dd = String(today.getDate()).padStart(2, '0');
    this.abonoDate = `${yyyy}-${mm}-${dd}`;
    this.abonoAmount = null;
    this.abonoMethod = '';
    this.abonoReceipt = '';
    this.abonoNotes = '';
  }

  private buildPipeline(detail: ApprenticeDetail | null): PipelineStep[] {
    if (!detail) {
      return [];
    }
    const p = detail.profile;
    const t = detail.training;
    const practical = detail.practical;
    const hoursDone = t.theoryHoursComplete && t.workshopHoursComplete;
    const examPassed = t.theoryExamPassed;
    const hasAppointment = !!detail.nextExam;
    const practicalDone =
      practical.requiredLessons > 0
      && practical.completedLessons >= practical.requiredLessons;

    return [
      {
        id: 'hours',
        label: 'Horas',
        detail: `Teoría ${t.theoryHoursCompleted}/${t.theoryHoursRequired} · Taller ${t.workshopHoursCompleted}/${t.workshopHoursRequired}`,
        state: hoursDone ? 'done' : t.theoryHoursCompleted > 0 || t.workshopHoursCompleted > 0 ? 'active' : 'todo'
      },
      {
        id: 'exam-auth',
        label: 'Autoriz. examen',
        detail: examPassed
          ? 'Ya aprobado'
          : p.theoryExamAuthorized
            ? 'Autorizado'
            : p.balanceDue > 0
              ? 'Saldo pendiente'
              : 'Sin autorizar',
        state: examPassed || p.theoryExamAuthorized
          ? 'done'
          : p.balanceDue > 0
            ? 'blocked'
            : 'todo'
      },
      {
        id: 'exam',
        label: 'Examen teórico',
        detail: examPassed
          ? 'Aprobado'
          : hasAppointment
            ? `${detail.nextExam!.examDate} ${detail.nextExam!.slotTime}`
            : 'Sin cita',
        state: examPassed ? 'done' : hasAppointment ? 'active' : p.theoryExamAuthorized ? 'todo' : 'blocked'
      },
      {
        id: 'practical-auth',
        label: 'Autoriz. manejo',
        detail: p.practicalAuthorized
          ? 'Autorizado'
          : !examPassed
            ? 'Requiere examen'
            : p.balanceDue > 0
              ? 'Saldo pendiente'
              : 'Sin autorizar',
        state: p.practicalAuthorized
          ? 'done'
          : examPassed
            ? p.balanceDue > 0
              ? 'blocked'
              : 'todo'
            : 'blocked'
      },
      {
        id: 'practical',
        label: 'Práctica',
        detail: `${practical.completedLessons}/${practical.requiredLessons || '—'} clases` +
          (practical.scheduledLessons ? ` · ${practical.scheduledLessons} prog.` : ''),
        state: practicalDone
          ? 'done'
          : practical.completedLessons > 0 || practical.scheduledLessons > 0
            ? 'active'
            : p.practicalAuthorized
              ? 'todo'
              : 'blocked'
      }
    ];
  }

  private buildTimeline(detail: ApprenticeDetail | null): TimelineItem[] {
    if (!detail) {
      return [];
    }
    const items: TimelineItem[] = [];
    const p = detail.profile;

    if (p.enrollmentDate || p.enrollmentMonth) {
      items.push({
        at: p.enrollmentDate || `${p.enrollmentMonth}-01`,
        title: 'Matrícula / ingreso',
        detail: [
          p.licenseCategories ? `Categoría ${p.licenseCategories}` : null,
          p.scheduleSlot ? `Horario ${p.scheduleSlot}` : null,
          p.attendanceDayType || null
        ]
          .filter(Boolean)
          .join(' · ') || 'Registro en la escuela',
        tone: 'info'
      });
    }

    for (const ev of detail.authorizationHistory ?? []) {
      items.push({
        at: ev.createdAt,
        title: this.authEventLabel(ev),
        detail: `Por ${ev.performedByName || 'escuela'}`,
        tone: ev.action === 'Granted' ? 'ok' : 'warn'
      });
    }

    for (const abono of detail.cartera?.abonos ?? []) {
      items.push({
        at: abono.paymentDate,
        title: `Abono ${this.formatMoney(abono.amount)}`,
        detail: [abono.paymentMethod, abono.receiptNumber ? `Recibo ${abono.receiptNumber}` : null]
          .filter(Boolean)
          .join(' · ') || (abono.recordedByName ? `Por ${abono.recordedByName}` : 'Pago registrado'),
        tone: 'ok'
      });
    }

    if (detail.nextExam) {
      items.push({
        at: detail.nextExam.examDate,
        title: 'Cita de examen teórico',
        detail: `${detail.nextExam.slotTime}`,
        tone: 'info'
      });
    }

    if (detail.practical.nextLessonDate) {
      items.push({
        at: detail.practical.nextLessonDate,
        title: 'Próxima clase de manejo',
        detail: detail.practical.nextLessonTime || '',
        tone: 'info'
      });
    }

    if (detail.training.theoryExamPassed) {
      items.push({
        at: new Date().toISOString(),
        title: 'Examen teórico aprobado',
        detail: 'Habilitado para continuar a práctica (según autorización)',
        tone: 'ok'
      });
    }

    if (p.balanceDue > 0) {
      items.push({
        at: new Date().toISOString(),
        title: 'Saldo pendiente',
        detail: this.formatMoney(p.balanceDue),
        tone: 'warn'
      });
    }

    return items.sort((a, b) => {
      const da = Date.parse(a.at) || 0;
      const db = Date.parse(b.at) || 0;
      return db - da;
    });
  }
}
