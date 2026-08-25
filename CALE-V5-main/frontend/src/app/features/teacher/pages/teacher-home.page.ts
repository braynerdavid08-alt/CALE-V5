import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiMotivationComponent } from '../../../shared/ui/ui-motivation.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiStatComponent } from '../../../shared/ui/ui-stat.component';
import { TeacherApi, TeacherDashboardDto } from '../api/teacher.api';

@Component({
  selector: 'app-teacher-home-page',
  standalone: true,
  imports: [
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiMotivationComponent,
    UiPageHeaderComponent,
    UiStatComponent
  ],
  templateUrl: './teacher-home.page.html',
  styleUrl: './teacher-home.page.css'
})
export class TeacherHomePage implements OnInit {
  private readonly api = inject(TeacherApi);
  readonly session = inject(SessionStore);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly data = signal<TeacherDashboardDto | null>(null);

  readonly displayName = computed(
    () => this.data()?.teacherName || this.session.user()?.name || 'Docente'
  );

  readonly schoolLine = computed(() => {
    const school = this.data()?.school;
    if (!school) {
      return 'Sin escuela asignada';
    }
    const place = [school.city, school.department].filter(Boolean).join(', ');
    return place ? `${school.legalName} · ${place}` : school.legalName;
  });

  ngOnInit(): void {
    this.api.dashboard().subscribe({
      next: (dto) => {
        this.data.set(dto);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  statusLabel(status: string): string {
    if (status === 'Active') return 'Membresía activa';
    if (status === 'PendingPayment') return 'Pago pendiente';
    if (status === 'Expired') return 'Membresía vencida';
    return status;
  }

  statusTone(status: string): 'success' | 'warning' | 'danger' | 'neutral' {
    if (status === 'Active') return 'success';
    if (status === 'PendingPayment') return 'warning';
    if (status === 'Expired') return 'danger';
    return 'neutral';
  }
}
