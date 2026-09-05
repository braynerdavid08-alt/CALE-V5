import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  NotificationDto,
  NotificationsApi
} from '../../../core/notifications/notifications.api';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiDashBarsComponent, DashBarItem } from '../../../shared/ui/ui-dash-bars.component';
import { UiDashKpiComponent } from '../../../shared/ui/ui-dash-kpi.component';
import { UiDashNotifsComponent } from '../../../shared/ui/ui-dash-notifs.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiOnboardingComponent } from '../../../shared/ui/ui-onboarding.component';
import { TeacherApi, TeacherDashboardDto } from '../api/teacher.api';
import { PresentationApi } from '../presentations/presentation.api';
import { PresentationSummary } from '../presentations/presentation.models';

@Component({
  selector: 'app-teacher-home-page',
  standalone: true,
  imports: [
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiDashBarsComponent,
    UiDashKpiComponent,
    UiDashNotifsComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiOnboardingComponent
  ],
  templateUrl: './teacher-home.page.html',
  styleUrl: './teacher-home.page.css'
})
export class TeacherHomePage implements OnInit {
  private readonly api = inject(TeacherApi);
  private readonly presentationsApi = inject(PresentationApi);
  private readonly notificationsApi = inject(NotificationsApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly session = inject(SessionStore);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly membershipHint = signal(false);
  readonly data = signal<TeacherDashboardDto | null>(null);
  readonly notifs = signal<NotificationDto[]>([]);
  readonly presentationSummary = signal<PresentationSummary | null>(null);

  readonly displayName = computed(
    () => this.data()?.teacherName || this.session.user()?.name || 'Instructor'
  );

  readonly schoolLine = computed(() => {
    const school = this.data()?.school;
    if (!school) {
      return 'Sin escuela asignada';
    }
    const place = [school.city, school.department].filter(Boolean).join(', ');
    return place ? `${school.legalName} · ${place}` : school.legalName;
  });

  readonly workloadBars = computed<DashBarItem[]>(() => {
    const d = this.data();
    if (!d) {
      return [];
    }
    const pending = d.pendingGrades?.length ?? 0;
    const low = d.lowScores?.length ?? 0;
    const published = d.publishedExams ?? 0;
    const totalExams = Math.max(d.totalExams ?? 0, 1);
    return [
      {
        label: 'Exámenes publicados',
        value: published,
        max: totalExams,
        tone: 'primary'
      },
      {
        label: 'Por calificar',
        value: pending,
        max: Math.max(pending, 5),
        tone: pending ? 'warning' : 'success'
      },
      {
        label: 'Bajo rendimiento',
        value: low,
        max: Math.max(low, 5),
        tone: low ? 'warning' : 'success'
      }
    ];
  });

  ngOnInit(): void {
    this.route.queryParamMap.subscribe((params) => {
      this.membershipHint.set(params.get('membresia') === '1');
    });

    forkJoin({
      dash: this.api.dashboard(),
      notifs: this.notificationsApi.list({ take: 6 }).pipe(
        catchError(() => of({ items: [] as NotificationDto[], unreadCount: 0 }))
      ),
      presentations: this.presentationsApi.summary().pipe(
        catchError(() => of({ total: 0, latest: null } as PresentationSummary))
      )
    }).subscribe({
      next: (res) => {
        this.data.set(res.dash);
        this.notifs.set(res.notifs.items);
        this.presentationSummary.set(res.presentations);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  relativePres(iso?: string | null): string {
    if (!iso) {
      return '';
    }
    const t = new Date(iso).getTime();
    if (Number.isNaN(t)) {
      return '';
    }
    const m = Math.floor((Date.now() - t) / 60000);
    if (m < 1) {
      return 'hace un momento';
    }
    if (m < 60) {
      return `hace ${m} min`;
    }
    const h = Math.floor(m / 60);
    if (h < 24) {
      return `hace ${h} h`;
    }
    return `hace ${Math.floor(h / 24)} d`;
  }

  statusLabel(status: string): string {
    if (status === 'Active') return 'Membresía activa';
    if (status === 'Expiring') return 'Por vencer';
    if (status === 'None') return 'Sin membresía';
    if (status === 'PendingPayment') return 'Pago pendiente';
    if (status === 'UnderReview' || status === 'PaymentSubmitted') return 'En revisión';
    if (status === 'Rejected') return 'Rechazada';
    if (status === 'Cancelled') return 'Cancelada';
    if (status === 'Suspended') return 'Suspendida';
    if (status === 'Expired') return 'Membresía vencida';
    return status;
  }

  statusTone(status: string): 'success' | 'warning' | 'danger' | 'neutral' | 'primary' {
    if (status === 'Active') return 'success';
    if (status === 'Expiring' || status === 'PendingPayment' || status === 'UnderReview' || status === 'PaymentSubmitted') {
      return 'warning';
    }
    if (status === 'Expired' || status === 'Rejected' || status === 'Suspended' || status === 'Cancelled') {
      return 'danger';
    }
    return 'neutral';
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
