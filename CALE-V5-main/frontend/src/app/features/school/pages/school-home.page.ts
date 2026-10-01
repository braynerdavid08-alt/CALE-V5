import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { SessionStore } from '../../../core/auth/session.store';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  NotificationDto,
  NotificationsApi
} from '../../../core/notifications/notifications.api';
import { ApprenticeApi, SchoolOperationsDashboard } from '../api/apprentice.api';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiDashNotifsComponent } from '../../../shared/ui/ui-dash-notifs.component';
import { InactiveStudentsCardComponent } from '../../play/components/inactive-students-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';

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
}

interface EasyAction {
  emoji: string;
  label: string;
  hint: string;
  path: string;
  query?: Record<string, string | boolean>;
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
    InactiveStudentsCardComponent,
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiDashNotifsComponent,
    UiErrorComponent,
    UiLoadingComponent
  ],
  templateUrl: './school-home.page.html',
  styleUrl: './school-home.page.css'
})
export class SchoolHomePage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly notificationsApi = inject(NotificationsApi);
  private readonly apprenticeApi = inject(ApprenticeApi);
  private readonly router = inject(Router);
  readonly session = inject(SessionStore);
  readonly free = this.session.freeAccess;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<SchoolProfileDto | null>(null);
  readonly notifs = signal<NotificationDto[]>([]);
  readonly ops = signal<SchoolOperationsDashboard | null>(null);
  readonly showAllTodos = signal(false);

  readonly actions: EasyAction[] = [
    { emoji: '➕', label: 'Inscribir estudiante', hint: 'Crear la cuenta de un estudiante nuevo', path: '/school/users' },
    { emoji: '✅', label: 'Tomar asistencia', hint: 'Marcar quién vino hoy', path: '/school/attendance' },
    { emoji: '💰', label: 'Cobrar saldos', hint: 'Ver quién debe y registrar pagos', path: '/school/apprentices', query: { withBalance: true } },
    { emoji: '📅', label: 'Exámenes', hint: 'Ver cupos de la semana y dar citas de examen', path: '/school/theory-exams' },
    { emoji: '🚗', label: 'Clases de manejo', hint: 'Programar prácticas en vehículo', path: '/school/practical' },
    { emoji: '📊', label: 'Ver resultados', hint: 'Notas de exámenes y simulacros', path: '/school/results' }
  ];

  /** Everything that needs attention, written as plain sentences with one action each. */
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
        text: o.readyForPracticalCount === 1
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
        text: o.pendingEnrollmentCount === 1
          ? '1 estudiante está pendiente de enrolar'
          : `${o.pendingEnrollmentCount} estudiantes están pendientes de enrolar`,
        action: 'Ver',
        link: '/school/apprentices',
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
      notifs: this.notificationsApi.list({ take: 5 }).pipe(
        catchError(() => of({ items: [] as NotificationDto[], unreadCount: 0 }))
      ),
      ops: this.apprenticeApi.getDashboard().pipe(
        catchError(() => of(null as SchoolOperationsDashboard | null))
      )
    }).subscribe({
      next: (res) => {
        this.profile.set(res.profile);
        this.notifs.set(res.notifs.items);
        this.ops.set(res.ops);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
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
    if (status === 'Rejected' || status === 'Expired' || status === 'Suspended' || status === 'Cancelled') {
      return 'danger';
    }
    return 'neutral';
  }

  formatMoney(value: number): string {
    return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(value);
  }

  openNotif(n: NotificationDto): void {
    const go = () => void this.router.navigateByUrl(n.link || '/notifications');
    if (n.isRead) {
      go();
      return;
    }
    this.notificationsApi.markRead(n.id).subscribe({
      next: () => {
        this.notifs.update((list) =>
          list.map((x) => (x.id === n.id ? { ...x, isRead: true } : x))
        );
        go();
      },
      error: () => go()
    });
  }
}
