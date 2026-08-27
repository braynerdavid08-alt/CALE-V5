import { Component, OnInit, computed, inject, signal } from '@angular/core';
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
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiDashDonutComponent, DashDonutSlice } from '../../../shared/ui/ui-dash-donut.component';
import { UiDashKpiComponent } from '../../../shared/ui/ui-dash-kpi.component';
import { UiDashLineComponent, DashLineSeries } from '../../../shared/ui/ui-dash-line.component';
import { UiDashNotifsComponent } from '../../../shared/ui/ui-dash-notifs.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiIconComponent } from '../../../shared/ui/ui-icon.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';

interface AdminDashboardDto {
  users: number;
  groups: number;
  attempts: number;
  questions: number;
  pendingRatings: number;
}

interface MonthlyPoint {
  label: string;
  year: number;
  month: number;
  students: number;
  teachers: number;
  schools: number;
}

interface PilotMetricsDto {
  dailyActiveUsers: number;
  weeklyActiveUsers: number;
  monthlyActiveUsers: number;
  activeSchools: number;
  pendingMembershipRequests: number;
  studentsTotal: number;
  teachersTotal: number;
  examPassRate30d: number;
  examCompletionRate30d: number;
  attemptsFinished30d: number;
  attemptsStarted30d: number;
  usersGrowth30d: number;
  schoolsGrowth30d: number;
  teachersGrowth30d: number;
  studentsGrowth30d: number;
  registrationsLast6Months: MonthlyPoint[];
}

interface MembershipRequestDto {
  userId: number;
  legalName: string;
  contactName: string;
  planLabel: string;
  displayStatus?: string;
  subscriptionStatus: string;
  hasPaymentProof: boolean;
  requestedAt?: string | null;
}

@Component({
  selector: 'app-admin-home-page',
  standalone: true,
  imports: [
    RouterLink,
    UiButtonComponent,
    UiDashDonutComponent,
    UiDashKpiComponent,
    UiDashLineComponent,
    UiDashNotifsComponent,
    UiErrorComponent,
    UiIconComponent,
    UiLoadingComponent
  ],
  templateUrl: './admin-home.page.html',
  styleUrl: './admin-home.page.css'
})
export class AdminHomePage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly notificationsApi = inject(NotificationsApi);
  private readonly router = inject(Router);
  readonly session = inject(SessionStore);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly dash = signal<AdminDashboardDto | null>(null);
  readonly metrics = signal<PilotMetricsDto | null>(null);
  readonly pending = signal<MembershipRequestDto[]>([]);
  readonly notifs = signal<NotificationDto[]>([]);

  readonly distribution = computed<DashDonutSlice[]>(() => {
    const m = this.metrics();
    const d = this.dash();
    if (!m && !d) {
      return [];
    }
    const students = m?.studentsTotal ?? 0;
    const teachers = m?.teachersTotal ?? 0;
    const schools = m?.activeSchools ?? 0;
    const admins = Math.max(0, (d?.users ?? 0) - students - teachers - schools);
    return [
      { label: 'Estudiantes', value: students, color: '#2f80ed' },
      { label: 'Instructores', value: teachers, color: '#7b61ff' },
      { label: 'Escuelas', value: schools, color: '#27ae60' },
      { label: 'Administradores', value: admins, color: '#b8a6ff' }
    ].filter((x) => x.value > 0);
  });

  readonly chartLabels = computed(() =>
    (this.metrics()?.registrationsLast6Months ?? []).map((p) => p.label)
  );

  readonly chartSeries = computed<DashLineSeries[]>(() => {
    const points = this.metrics()?.registrationsLast6Months ?? [];
    if (!points.length) {
      return [];
    }
    return [
      {
        label: 'Estudiantes',
        color: '#2f80ed',
        values: points.map((p) => p.students)
      },
      {
        label: 'Escuelas',
        color: '#27ae60',
        values: points.map((p) => p.schools)
      }
    ];
  });

  ngOnInit(): void {
    forkJoin({
      dash: this.http.get<AdminDashboardDto>(`${env.apiUrl}/api/admin/dashboard`),
      metrics: this.http
        .get<PilotMetricsDto>(`${env.apiUrl}/api/admin/metrics`)
        .pipe(catchError(() => of(null))),
      pending: this.http
        .get<MembershipRequestDto[]>(`${env.apiUrl}/api/admin/memberships/pending`)
        .pipe(catchError(() => of([] as MembershipRequestDto[]))),
      notifs: this.notificationsApi.list({ take: 5 }).pipe(
        catchError(() => of({ items: [] as NotificationDto[], unreadCount: 0 }))
      )
    }).subscribe({
      next: (res) => {
        this.dash.set(res.dash);
        this.metrics.set(res.metrics);
        this.pending.set(res.pending.slice(0, 5));
        this.notifs.set(res.notifs.items);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  formatDelta(value: number | undefined | null): string | null {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return null;
    }
    const sign = value > 0 ? '+' : '';
    return `${sign}${value}% este mes`;
  }

  openNotif(n: NotificationDto): void {
    const go = () => {
      void this.router.navigateByUrl(n.link || '/notifications');
    };
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
