import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { SessionStore } from '../../../core/auth/session.store';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { ApprenticeApi, SchoolOperationsDashboard } from '../api/apprentice.api';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { ShopOffersWidgetComponent } from '../../public/shop-offers-widget.component';

interface SchoolProfileDto {
  contactName: string;
  legalName: string;
  planLabel: string;
  subscriptionStatus: string;
  displayStatus?: string;
  renewalStatus?: string;
  membershipEndsAt?: string | null;
  daysRemaining: number;
  isMembershipActive: boolean;
  teachersUsed: number;
  teachersMax: number;
  studentsUsed: number;
  studentsMax: number;
  pendingApproval?: boolean;
}

interface EasyAction {
  emoji: string;
  label: string;
  hint: string;
  path: string;
  query?: Record<string, string | boolean>;
}

interface OpMetric {
  value: string;
  label: string;
}

interface OpCard {
  key: string;
  emoji: string;
  label: string;
  hint: string;
  path: string;
  query?: Record<string, string | boolean>;
  metrics: OpMetric[];
}

interface TodoItem {
  key: string;
  emoji: string;
  text: string;
  action: string;
  link: string | (string | number)[];
  query?: Record<string, string | boolean>;
  tone: 'warn' | 'good' | 'info';
}

const VISIBLE_TODOS = 6;

@Component({
  selector: 'app-school-home-page',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent,
    ShopOffersWidgetComponent
  ],
  templateUrl: './school-home.page.html',
  styleUrl: './school-home.page.css'
})
export class SchoolHomePage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly apprenticeApi = inject(ApprenticeApi);
  readonly session = inject(SessionStore);
  readonly free = this.session.freeAccess;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<SchoolProfileDto | null>(null);
  readonly ops = signal<SchoolOperationsDashboard | null>(null);
  readonly showAllTodos = signal(false);
  readonly exporting = signal(false);

  /**
   * Secondary tools only — never repeat Estudiantes / Programación / Cartera / Operación de hoy.
   * Export is a real download (not Importar Excel).
   */
  readonly secondaryActions: EasyAction[] = [
    { emoji: '🗓️', label: 'Agenda semanal', hint: 'Vista de la semana', path: '/school/agenda' },
    {
      emoji: '📑',
      label: 'Reportes de programación',
      hint: 'Excel del día · WhatsApp próximamente',
      path: '/school/reports'
    },
    {
      emoji: '📝',
      label: 'Exámenes teóricos',
      hint: 'Cupos y citas',
      path: '/school/theory-exams'
    },
    {
      emoji: '⏰',
      label: 'Crear horarios disponibles',
      hint: 'Plantilla de cupos de examen',
      path: '/school/theory-exams/schedule'
    },
    { emoji: '🚗', label: 'Clases de manejo', hint: 'Programar prácticas y examen', path: '/school/practical' },
    { emoji: '📊', label: 'Resultados', hint: 'Notas y progreso', path: '/school/results' },
    { emoji: '👥', label: 'Instructores y usuarios', hint: 'Invitar y gestionar', path: '/school/users' },
    { emoji: '📥', label: 'Importar expedientes', hint: 'Carga masiva Excel', path: '/school/import' },
    { emoji: '💬', label: 'PQR y mis datos', hint: 'Solicitudes', path: '/mis-pqr' },
    { emoji: '🛒', label: 'Tienda y equipamiento', hint: 'Cascos y equipo', path: '/tienda' },
    { emoji: '💡', label: 'Proponer y sugerir', hint: 'Ideas para Luz Verde', path: '/solicitudes' },
    {
      emoji: '💚',
      label: 'Apoyar Luz Verde',
      hint: 'Donación voluntaria',
      path: '/donaciones'
    }
  ];

  readonly visibleSecondaryActions = computed(() => {
    const free = this.free();
    return this.secondaryActions.filter((a) => {
      if (a.path === '/donaciones' && !free) {
        return false;
      }
      return true;
    });
  });

  /** Four main cards: access + real KPIs from school dashboard / profile. */
  readonly operationCards = computed<OpCard[]>(() => {
    const o = this.ops();
    const p = this.profile();
    const today = this.todayKey();
    const examsToday =
      o?.upcomingExams?.filter((e) => e.examDate === today).length ?? null;

    const studentsValue =
      o?.apprenticeCount ?? p?.studentsUsed ?? null;
    const balanceTotal = o?.balancePendingTotal ?? null;
    const balanceCount = o?.balancePendingCount ?? null;
    const examsWeek = o?.examsNext7Days ?? null;
    const readyPractical = o?.readyForPracticalCount ?? null;

    return [
      {
        key: 'students',
        emoji: '🎓',
        label: 'Estudiantes',
        hint: 'Expedientes, progreso y autorizaciones',
        path: '/school/apprentices',
        metrics: studentsValue != null
          ? [{ value: String(studentsValue), label: 'en expediente' }]
          : []
      },
      {
        key: 'program',
        emoji: '📅',
        label: 'Programación',
        hint: 'Clases teóricas, exámenes y manejo',
        path: '/school/agenda',
        metrics: [
          ...(examsWeek != null
            ? [{ value: String(examsWeek), label: 'exámenes (7 días)' }]
            : []),
          ...(readyPractical != null && readyPractical > 0
            ? [{ value: String(readyPractical), label: 'listos para manejo' }]
            : [])
        ]
      },
      {
        key: 'cartera',
        emoji: '💰',
        label: 'Cartera',
        hint: 'Quién debe y registro de pagos',
        path: '/school/apprentices',
        query: { withBalance: true },
        metrics:
          balanceTotal != null && balanceTotal > 0
            ? [
                { value: this.formatMoney(balanceTotal), label: 'pendiente' },
                ...(balanceCount != null
                  ? [{ value: String(balanceCount), label: 'con saldo' }]
                  : [])
              ]
            : balanceCount != null
              ? [{ value: String(balanceCount), label: 'con saldo' }]
              : [{ value: 'Al día', label: 'sin saldos pendientes' }]
      },
      {
        key: 'today',
        emoji: '✅',
        label: 'Operación de hoy',
        hint: 'Asistencia, exámenes y clases en tiempo real',
        path: '/school/attendance',
        metrics: [
          ...(examsToday != null
            ? [{ value: String(examsToday), label: 'exámenes hoy' }]
            : []),
          ...(o?.noExamAppointmentCount != null && o.noExamAppointmentCount > 0
            ? [
                {
                  value: String(o.noExamAppointmentCount),
                  label: 'sin cita de examen'
                }
              ]
            : []),
          ...(o?.pendingEnrollmentCount != null && o.pendingEnrollmentCount > 0
            ? [
                {
                  value: String(o.pendingEnrollmentCount),
                  label: 'sin enrolar'
                }
              ]
            : [])
        ]
      }
    ];
  });

  readonly todos = computed<TodoItem[]>(() => {
    const o = this.ops();
    if (!o) return [];
    const items: TodoItem[] = [];
    for (const row of o.topBalanceDue) {
      items.push({
        key: `bal-${row.studentUserId}`,
        emoji: '💰',
        text: `${row.studentName} debe ${this.formatMoney(row.balanceDue)}`,
        action: 'Cobrar',
        link: ['/school/apprentices', row.studentUserId],
        tone: 'warn'
      });
    }
    if (o.balancePendingCount > o.topBalanceDue.length) {
      items.push({
        key: 'bal-more',
        emoji: '💰',
        text: `${o.balancePendingCount} estudiantes tienen saldo pendiente (${this.formatMoney(o.balancePendingTotal)} en total)`,
        action: 'Ver todos',
        link: '/school/apprentices',
        query: { withBalance: true },
        tone: 'warn'
      });
    }
    for (const row of o.topReadyForExam) {
      items.push({
        key: `ready-${row.studentUserId}`,
        emoji: '🎓',
        text: `${row.studentName} terminó la teoría: ya puedes autorizar su examen`,
        action: 'Autorizar',
        link: '/school/training',
        tone: 'good'
      });
    }
    for (const row of o.topNoExamAppointment) {
      items.push({
        key: `noexam-${row.studentUserId}`,
        emoji: '📅',
        text: `${row.studentName} está autorizado pero no tiene fecha de examen`,
        action: 'Dar cita',
        link: '/school/theory-exams',
        tone: 'warn'
      });
    }
    if (o.readyForPracticalCount > 0) {
      items.push({
        key: 'practical',
        emoji: '🚗',
        text:
          o.readyForPracticalCount === 1
            ? '1 estudiante aprobó el examen teórico y puede empezar clases de manejo'
            : `${o.readyForPracticalCount} estudiantes aprobaron el examen teórico y pueden empezar clases de manejo`,
        action: 'Programar',
        link: '/school/practical',
        tone: 'good'
      });
    }
    if (o.pendingEnrollmentCount > 0) {
      items.push({
        key: 'enroll',
        emoji: '📝',
        text:
          o.pendingEnrollmentCount === 1
            ? '1 estudiante está pendiente de enrolar'
            : `${o.pendingEnrollmentCount} estudiantes están pendientes de enrolar`,
        action: 'Ver',
        link: '/school/apprentices',
        query: { pendingEnrollment: true },
        tone: 'info'
      });
    }
    for (const exam of o.upcomingExams) {
      items.push({
        key: `exam-${exam.id}`,
        emoji: '🗓️',
        text: `Examen el ${exam.examDate} a las ${exam.slotTime}: ${exam.studentName || exam.studentLabel || 'sin estudiante asignado'}`,
        action: 'Ver',
        link: '/school/theory-exams',
        tone: 'info'
      });
    }
    return items;
  });

  readonly visibleTodos = computed(() =>
    this.showAllTodos() ? this.todos() : this.todos().slice(0, VISIBLE_TODOS)
  );

  ngOnInit(): void {
    forkJoin({
      profile: this.http.get<SchoolProfileDto>(`${env.apiUrl}/api/school/profile`),
      ops: this.apprenticeApi.getDashboard().pipe(
        catchError(() => of(null as SchoolOperationsDashboard | null))
      )
    }).subscribe({
      next: (res) => {
        this.profile.set(res.profile);
        this.ops.set(res.ops);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  exportApprenticesExcel(): void {
    if (this.exporting()) {
      return;
    }
    this.exporting.set(true);
    this.error.set(null);
    this.apprenticeApi.exportApprenticesXlsx().subscribe({
      next: (blob) => {
        this.exporting.set(false);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `estudiantes-luz-verde-${this.todayKey()}.xlsx`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => {
        this.exporting.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  statusLabel(status: string): string {
    if (status === 'Active') return 'Activo';
    if (status === 'Expiring') return 'Por vencer';
    if (status === 'None') return 'Sin membresía';
    if (status === 'PendingPayment') return 'Pendiente de pago';
    if (status === 'UnderReview' || status === 'PaymentSubmitted') return 'En revisión';
    if (status === 'Rejected') return 'Solicitud rechazada';
    if (status === 'Cancelled') return 'Cancelada';
    if (status === 'Suspended') return 'Suspendida';
    if (status === 'Expired') return 'Vencido';
    return status;
  }

  statusTone(status: string): 'success' | 'warning' | 'danger' | 'neutral' | 'primary' {
    if (status === 'Active') return 'success';
    if (status === 'Expiring' || status === 'PendingPayment') return 'warning';
    if (status === 'UnderReview' || status === 'PaymentSubmitted') return 'primary';
    if (
      status === 'Rejected' ||
      status === 'Expired' ||
      status === 'Suspended' ||
      status === 'Cancelled'
    ) {
      return 'danger';
    }
    return 'neutral';
  }

  formatMoney(value: number): string {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      maximumFractionDigits: 0
    }).format(value);
  }

  private todayKey(): string {
    const d = new Date();
    const y = d.getFullYear();
    const m = `${d.getMonth() + 1}`.padStart(2, '0');
    const day = `${d.getDate()}`.padStart(2, '0');
    return `${y}-${m}-${day}`;
  }
}
