import { Component, inject, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiStatComponent } from '../../../shared/ui/ui-stat.component';

@Component({
  selector: 'app-admin-users-page',
  standalone: true,
  imports: [
    RouterLink,
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
      title="Usuarios"
      subtitle="Conteo real. El listado detallado no está expuesto por la API." />
    <ui-error [message]="error()" />
    @if (loading()) {
      <ui-loading />
    } @else {
      <div class="grid-stats">
        <ui-stat label="Usuarios registrados" [value]="users()" tone="primary" />
      </div>
      <ui-card>
        <ui-empty
          title="Listado no disponible"
          message="Este problema parece provenir del backend y no será modificado como parte del rediseño: no existe un endpoint de gestión de usuarios.">
          <p class="muted">Los estudiantes se crean con el registro público. Docentes y admin se gestionan en seed/base.</p>
          <p><a routerLink="/profile">Ir a mi perfil</a></p>
        </ui-empty>
      </ui-card>
    }
  `
})
export class AdminUsersPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly users = signal(0);

  ngOnInit(): void {
    this.http.get<{ users: number }>(`${env.apiUrl}/api/admin/dashboard`)
      .subscribe({
        next: (dto) => {
          this.users.set(dto.users);
          this.loading.set(false);
        },
        error: (err) => {
          this.loading.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }
}
