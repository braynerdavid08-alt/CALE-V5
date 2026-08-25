import { Component, inject, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiStatComponent } from '../../../shared/ui/ui-stat.component';

interface AdminDashboardDto {
  users: number;
  groups: number;
  attempts: number;
  questions: number;
  pendingRatings: number;
}

@Component({
  selector: 'app-admin-home-page',
  standalone: true,
  imports: [
    RouterLink,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiStatComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Administración"
      title="Dashboard"
      subtitle="Estado real de la plataforma." />
    <ui-error [message]="error()" />
    @if (loading()) {
      <ui-loading />
    } @else if (data()) {
      <div class="grid-stats">
        <ui-stat label="Usuarios" [value]="data()!.users" tone="primary" />
        <ui-stat label="Preguntas" [value]="data()!.questions" />
        <ui-stat label="Intentos" [value]="data()!.attempts" />
        <ui-stat label="Grupos" [value]="data()!.groups" />
        <ui-stat label="Valoraciones" [value]="data()!.pendingRatings" tone="warning" />
      </div>
      <ui-card>
        <h2>Acciones rápidas</h2>
        <div class="row">
          <a routerLink="/admin/questions/new"><ui-button type="button">Nueva pregunta</ui-button></a>
          <a routerLink="/admin/exams"><ui-button type="button" variant="secondary">Exámenes</ui-button></a>
          <a routerLink="/admin/results"><ui-button type="button" variant="secondary">Resultados</ui-button></a>
          <a routerLink="/teacher/groups"><ui-button type="button" variant="secondary">Grupos</ui-button></a>
        </div>
      </ui-card>
    } @else {
      <ui-empty title="Sin métricas" message="No hay datos todavía." />
    }
  `
})
export class AdminHomePage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly data = signal<AdminDashboardDto | null>(null);

  ngOnInit(): void {
    this.http.get<AdminDashboardDto>(`${env.apiUrl}/api/admin/dashboard`)
      .subscribe({
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
}
