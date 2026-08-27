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
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiDashBarsComponent, DashBarItem } from '../../../shared/ui/ui-dash-bars.component';
import { UiDashKpiComponent } from '../../../shared/ui/ui-dash-kpi.component';
import { UiDashNotifsComponent } from '../../../shared/ui/ui-dash-notifs.component';
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

interface MembershipEventDto {
  eventType: string;
  note?: string | null;
  createdAt: string;
}

interface UserRow {
  id: number;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
}

@Component({
  selector: 'app-school-home-page',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiDashBarsComponent,
    UiDashKpiComponent,
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
  private readonly router = inject(Router);
  readonly session = inject(SessionStore);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<SchoolProfileDto | null>(null);
  readonly events = signal<MembershipEventDto[]>([]);
  readonly members = signal<UserRow[]>([]);
  readonly notifs = signal<NotificationDto[]>([]);

  readonly seatBars = computed<DashBarItem[]>(() => {
    const p = this.profile();
    if (!p) {
      return [];
    }
    return [
      {
        label: 'Cupos instructores',
        value: p.teachersUsed,
        max: Math.max(p.teachersMax, 1),
        tone: 'info'
      },
      {
        label: 'Cupos estudiantes',
        value: p.studentsUsed,
        max: Math.max(p.studentsMax, 1),
        tone: 'primary'
      }
    ];
  });

  readonly activeTeachers = computed(
    () => this.members().filter((m) => m.role === 'Teacher' && m.isActive).length
  );
  readonly activeStudents = computed(
    () => this.members().filter((m) => m.role === 'Student' && m.isActive).length
  );

  ngOnInit(): void {
    forkJoin({
      profile: this.http.get<SchoolProfileDto>(`${env.apiUrl}/api/school/profile`),
      events: this.http
        .get<MembershipEventDto[]>(`${env.apiUrl}/api/school/plan/history`)
        .pipe(catchError(() => of([] as MembershipEventDto[]))),
      members: this.http
        .get<UserRow[]>(`${env.apiUrl}/api/school/members`)
        .pipe(catchError(() => of([] as UserRow[]))),
      notifs: this.notificationsApi.list({ take: 5 }).pipe(
        catchError(() => of({ items: [] as NotificationDto[], unreadCount: 0 }))
      )
    }).subscribe({
      next: (res) => {
        this.profile.set(res.profile);
        this.events.set(res.events.slice(0, 6));
        this.members.set(res.members);
        this.notifs.set(res.notifs.items);
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

  eventLabel(type: string): string {
    const map: Record<string, string> = {
      Requested: 'Solicitud creada',
      ProofSubmitted: 'Comprobante enviado',
      Activated: 'Membresía activada',
      Renewed: 'Renovación',
      Rejected: 'Rechazada',
      Cancelled: 'Cancelada',
      Suspended: 'Suspendida',
      Unsuspended: 'Reactivada',
      SeatsAdjusted: 'Cupos ajustados',
      MembershipOverridden: 'Ajuste admin',
      RequestReopened: 'Solicitud reabierta'
    };
    return map[type] || type;
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
