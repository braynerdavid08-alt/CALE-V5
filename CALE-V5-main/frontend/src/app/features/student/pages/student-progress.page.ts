import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  PracticalApi,
  PracticalStudentDashboardDto
} from '../../practical/api/practical.api';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { StudentTheoryApi, TheoryStudentDashboardDto } from '../api/student-theory.api';

type ItemState = 'done' | 'todo' | 'blocked' | 'info';

interface ChecklistItem {
  id: string;
  title: string;
  detail: string;
  state: ItemState;
  ctaLabel?: string;
  ctaLink?: string;
}

@Component({
  selector: 'app-student-progress-page',
  standalone: true,
  imports: [
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  templateUrl: './student-progress.page.html',
  styleUrl: './student-progress.page.css'
})
export class StudentProgressPage implements OnInit {
  private readonly theoryApi = inject(StudentTheoryApi);
  private readonly practicalApi = inject(PracticalApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly theory = signal<TheoryStudentDashboardDto | null>(null);
  readonly practical = signal<PracticalStudentDashboardDto | null>(null);

  readonly items = computed(() => this.buildChecklist(this.theory(), this.practical()));
  readonly openCount = computed(() => this.items().filter((i) => i.state !== 'done').length);
  readonly nextHint = computed(() => {
    const open = this.items().find((i) => i.state === 'todo' || i.state === 'blocked');
    return open?.detail ?? 'Vas al día con tu formación.';
  });

  ngOnInit(): void {
    forkJoin({
      theory: this.theoryApi.dashboard().pipe(catchError(() => of(null))),
      practical: this.practicalApi.studentDashboard().pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ theory, practical }) => {
        this.theory.set(theory);
        this.practical.set(practical);
        this.loading.set(false);
        if (!theory && !practical) {
          this.error.set('No se pudo cargar tu progreso. Intenta de nuevo.');
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  formatMoney(value: number): string {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      maximumFractionDigits: 0
    }).format(value || 0);
  }

  private buildChecklist(
    theory: TheoryStudentDashboardDto | null,
    practical: PracticalStudentDashboardDto | null
  ): ChecklistItem[] {
    const pe = theory?.practicalEligibility ?? practical?.eligibility ?? null;
    const balanceDue = Number(theory?.balanceDue ?? 0);
    const completedLessons = Number(practical?.completedLessons ?? 0);
    const requiredLessons = Number(practical?.requiredLessons ?? 0);
    const items: ChecklistItem[] = [];

    const theoryDone = !!pe?.theoryHoursComplete;
    items.push({
      id: 'theory-hours',
      title: 'Horas de teoría',
      detail: theory
        ? `${theory.hoursCompleted}/${theory.hoursRequired} h`
        : pe
          ? `${pe.theoryHoursCompleted}/${pe.theoryHoursRequired} h`
          : 'Sin datos',
      state: theoryDone ? 'done' : 'todo',
      ctaLabel: theoryDone ? undefined : 'Ir a Teoría',
      ctaLink: theoryDone ? undefined : '/student/training'
    });

    const workshopDone = !!pe?.workshopHoursComplete;
    items.push({
      id: 'workshop-hours',
      title: 'Horas de taller',
      detail: theory
        ? `${theory.workshopHoursCompleted}/${theory.workshopHoursRequired} h`
        : pe
          ? `${pe.workshopHoursCompleted}/${pe.workshopHoursRequired} h`
          : 'Sin datos',
      state: workshopDone ? 'done' : 'todo',
      ctaLabel: workshopDone ? undefined : 'Ir a Teoría',
      ctaLink: workshopDone ? undefined : '/student/training'
    });

    items.push({
      id: 'balance',
      title: 'Saldo al día',
      detail:
        balanceDue > 0
          ? `Pendiente ${this.formatMoney(balanceDue)}. Consulta en tu escuela.`
          : 'Sin saldo pendiente',
      state: balanceDue > 0 ? 'blocked' : 'done'
    });

    const examAuth = !!pe?.theoryExamAuthorized || !!pe?.theoryExamPassed;
    items.push({
      id: 'exam-auth',
      title: 'Autorización de examen teórico',
      detail: pe?.theoryExamPassed
        ? 'Ya no es necesaria (examen aprobado)'
        : examAuth
          ? 'Autorizado por tu escuela'
          : 'Tu escuela debe autorizarte',
      state: examAuth ? 'done' : 'blocked'
    });

    const examPassed = !!pe?.theoryExamPassed;
    const examDetail = examPassed
      ? 'Aprobado'
      : theory?.nextExamAppointment
        ? `Cita ${theory.nextExamAppointment.examDate} ${theory.nextExamAppointment.slotTime}`
        : theory?.platformExam
          ? `Presenta «${theory.platformExam.name}» en el simulador`
          : 'Sin cita ni examen configurado';
    items.push({
      id: 'exam-pass',
      title: 'Aprobar examen teórico',
      detail: examDetail,
      state: examPassed ? 'done' : examAuth ? 'todo' : 'blocked',
      ctaLabel: examPassed || !examAuth ? undefined : 'Ir al simulador',
      ctaLink: examPassed || !examAuth ? undefined : '/student/simulator'
    });

    const practicalAuth = !!pe?.practicalAuthorized;
    items.push({
      id: 'practical-auth',
      title: 'Autorización de clases de manejo',
      detail: practicalAuth
        ? 'Autorizado por tu escuela'
        : !examPassed
          ? 'Requiere examen teórico aprobado'
          : 'Tu escuela debe autorizarte',
      state: practicalAuth ? 'done' : 'blocked'
    });

    const lessonsDone = requiredLessons > 0 && completedLessons >= requiredLessons;
    items.push({
      id: 'practical-lessons',
      title: 'Clases de manejo',
      detail:
        requiredLessons > 0
          ? `${completedLessons}/${requiredLessons} clases`
          : practical?.nextLesson
            ? `Próxima: ${practical.nextLesson.sessionDate} ${practical.nextLesson.startTime?.slice(0, 5)}`
            : pe?.canBookPractical
              ? 'Ya puedes reservar'
              : 'Aún no habilitado',
      state: lessonsDone
        ? 'done'
        : pe?.canBookPractical
          ? 'todo'
          : 'blocked',
      ctaLabel: pe?.canBookPractical && !lessonsDone ? 'Reservar manejo' : undefined,
      ctaLink: pe?.canBookPractical && !lessonsDone ? '/student/practical' : undefined
    });

    if (theory?.nextClass) {
      items.push({
        id: 'next-theory',
        title: 'Próxima clase teórica',
        detail: `${theory.nextClass.sessionDate} ${String(theory.nextClass.startTime).slice(0, 5)}${
          theory.nextClass.topicName ? ` · ${theory.nextClass.topicName}` : ''
        }`,
        state: 'info',
        ctaLabel: 'Ver formación',
        ctaLink: '/student/training'
      });
    }

    if (practical?.nextLesson) {
      items.push({
        id: 'next-practical',
        title: 'Próxima clase de manejo',
        detail: `${practical.nextLesson.sessionDate} ${practical.nextLesson.startTime.slice(0, 5)} · ${practical.nextLesson.vehicleLabel}`,
        state: 'info',
        ctaLabel: 'Ver práctica',
        ctaLink: '/student/practical'
      });
    }

    return items;
  }
}
