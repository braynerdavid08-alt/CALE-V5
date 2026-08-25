import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiStatComponent } from '../../../shared/ui/ui-stat.component';

interface SchoolProfileDto {
  contactName: string;
  legalName: string;
  planLabel: string;
  subscriptionStatus: string;
  membershipEndsAt?: string | null;
  daysRemaining: number;
  isMembershipActive: boolean;
  teachersUsed: number;
  teachersMax: number;
  studentsUsed: number;
  studentsMax: number;
}

@Component({
  selector: 'app-school-home-page',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiStatComponent
  ],
  styles: [`
    .muted { color: var(--color-text-secondary); margin: 0.35rem 0 0; }
    .actions { margin-top: 1rem; display: flex; flex-wrap: wrap; gap: 0.75rem; }
    .hello { margin: 0; font-size: var(--text-lg); font-weight: 700; }
  `],
  template: `
    <ui-page-header
      eyebrow="Escuela"
      title="Inicio"
      subtitle="Resumen de tu institución y accesos rápidos." />

    <ui-error [message]="error()" />

    @if (loading()) {
      <ui-loading />
    } @else if (profile()) {
      <div class="grid-stats">
        <ui-stat
          label="Días restantes"
          [value]="profile()!.daysRemaining"
          [tone]="profile()!.isMembershipActive ? 'success' : 'warning'" />
        <ui-stat
          label="Docentes"
          [value]="profile()!.teachersUsed + ' / ' + profile()!.teachersMax"
          tone="primary" />
        <ui-stat
          label="Estudiantes"
          [value]="profile()!.studentsUsed + ' / ' + profile()!.studentsMax" />
        <ui-stat label="Plan" [value]="profile()!.planLabel" />
      </div>

      <div class="grid-2">
        <ui-card>
          <h2>Bienvenida</h2>
          <p class="hello">{{ profile()!.legalName || session.user()?.name }}</p>
          <p class="muted">
            Plan {{ profile()!.planLabel }} ·
            <ui-badge [tone]="statusTone(profile()!.subscriptionStatus)">
              {{ statusLabel(profile()!.subscriptionStatus) }}
            </ui-badge>
          </p>
          @if (profile()!.membershipEndsAt) {
            <p class="muted">
              Vence el {{ profile()!.membershipEndsAt | date:'mediumDate' }}
              ({{ profile()!.daysRemaining }} día(s)).
            </p>
          }
          <div class="actions">
            <a routerLink="/school/membership">
              <ui-button type="button">Gestionar membresía</ui-button>
            </a>
            <a routerLink="/school/users">
              <ui-button type="button" variant="secondary">Usuarios</ui-button>
            </a>
          </div>
        </ui-card>

        <ui-card>
          <h2>Catálogo</h2>
          <p class="muted">
            Consulta las preguntas y bancos oficiales heredados de la plataforma (solo lectura).
          </p>
          <div class="actions">
            <a routerLink="/school/questions">
              <ui-button type="button" variant="secondary">Preguntas</ui-button>
            </a>
            <a routerLink="/school/banks">
              <ui-button type="button" variant="secondary">Bancos</ui-button>
            </a>
          </div>
        </ui-card>
      </div>
    }
  `
})
export class SchoolHomePage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly session = inject(SessionStore);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<SchoolProfileDto | null>(null);

  ngOnInit(): void {
    this.http.get<SchoolProfileDto>(`${env.apiUrl}/api/school/profile`).subscribe({
      next: (dto) => {
        this.profile.set(dto);
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
    if (status === 'PendingPayment') return 'Pago pendiente';
    if (status === 'Expired') return 'Vencido';
    return status;
  }

  statusTone(status: string): 'success' | 'warning' | 'danger' | 'neutral' {
    if (status === 'Active') return 'success';
    if (status === 'PendingPayment') return 'warning';
    if (status === 'Expired') return 'danger';
    return 'neutral';
  }
}
