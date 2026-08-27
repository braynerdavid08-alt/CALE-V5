import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  NotificationDto,
  NotificationsApi
} from '../../../core/notifications/notifications.api';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiDashBarsComponent, DashBarItem } from '../../../shared/ui/ui-dash-bars.component';
import { UiDashKpiComponent } from '../../../shared/ui/ui-dash-kpi.component';
import { UiDashNotifsComponent } from '../../../shared/ui/ui-dash-notifs.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { itemStatusLabel } from '../../../shared/utils/item-status-label';
import { StudentApi, StudentDashboardDto } from '../api/student.api';

interface ResultRow {
  attemptId: number;
  percent: number;
  passed: boolean;
  mode: string;
  finishedAt?: string | null;
}

@Component({
  selector: 'app-student-home-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiDashBarsComponent,
    UiDashKpiComponent,
    UiDashNotifsComponent,
    UiErrorComponent,
    UiLoadingComponent
  ],
  templateUrl: './student-home.page.html',
  styleUrl: './student-home.page.css'
})
export class StudentHomePage implements OnInit {
  private readonly api = inject(StudentApi);
  private readonly notificationsApi = inject(NotificationsApi);
  private readonly router = inject(Router);
  readonly session = inject(SessionStore);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly data = signal<StudentDashboardDto | null>(null);
  readonly notifs = signal<NotificationDto[]>([]);
  readonly results = signal<ResultRow[]>([]);
  readonly statusLabel = itemStatusLabel;
  code = '';

  readonly progressBars = computed<DashBarItem[]>(() => {
    const best = this.data()?.bestPercent;
    const pending = this.data()?.pendingActivities?.length ?? 0;
    const groups = this.data()?.groups?.length ?? 0;
    const passed = this.results().filter((r) => r.passed).length;
    const total = Math.max(this.results().length, 1);
    return [
      {
        label: 'Mejor marca simulador',
        value: Math.round(Number(best ?? 0)),
        max: 100,
        tone: 'success'
      },
      {
        label: 'Evaluaciones aprobadas',
        value: passed,
        max: total,
        tone: 'primary'
      },
      {
        label: 'Grupos activos',
        value: groups,
        max: Math.max(groups, 3),
        tone: 'info'
      },
      {
        label: 'Actividades pendientes',
        value: pending,
        max: Math.max(pending, 5),
        tone: pending > 0 ? 'warning' : 'success'
      }
    ];
  });

  readonly recentResults = computed(() => this.results().slice(0, 5));

  readonly passedLabel = computed(() => {
    const rows = this.results();
    const passed = rows.filter((r) => r.passed).length;
    return `${passed} / ${Math.max(rows.length, 0)}`;
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      dash: this.api.dashboard(),
      notifs: this.notificationsApi.list({ take: 6 }).pipe(
        catchError(() => of({ items: [] as NotificationDto[], unreadCount: 0 }))
      ),
      results: this.api.results().pipe(catchError(() => of([] as ResultRow[])))
    }).subscribe({
      next: (res) => {
        this.data.set(res.dash);
        this.notifs.set(res.notifs.items);
        this.results.set(res.results);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  join(): void {
    if (!this.code.trim()) {
      return;
    }
    this.api.joinGroup(this.code.trim()).subscribe({
      next: () => {
        this.code = '';
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
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
