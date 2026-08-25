import { Component, inject, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';

interface ResultRow {
  attemptId: number;
  userName: string;
  percent: number;
  passed: boolean;
  mode: string;
}

@Component({
  selector: 'app-admin-results-page',
  standalone: true,
  imports: [
    UiBadgeComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Administración"
      title="Resultados"
      subtitle="Intentos finalizados con datos reales." />
    <ui-error [message]="error()" />
    @if (loading()) {
      <ui-loading />
    } @else if (!items().length) {
      <ui-empty title="No hay intentos finalizados" message="Cuando alguien termine un examen verás el resultado aquí." />
    } @else {
      <ui-card>
        <div class="table-wrap">
          <table class="data">
            <thead>
              <tr>
                <th>Usuario</th>
                <th>Modo</th>
                <th>Porcentaje</th>
                <th>Estado</th>
              </tr>
            </thead>
            <tbody>
              @for (item of items(); track item.attemptId) {
                <tr>
                  <td>{{ item.userName }}</td>
                  <td>{{ item.mode }}</td>
                  <td>{{ item.percent }}%</td>
                  <td>
                    <ui-badge [tone]="item.passed ? 'success' : 'danger'">
                      {{ item.passed ? 'Aprobado' : 'No aprobado' }}
                    </ui-badge>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </ui-card>
    }
  `
})
export class AdminResultsPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly items = signal<ResultRow[]>([]);
  readonly error = signal<string | null>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.http.get<ResultRow[]>(`${env.apiUrl}/api/admin/results`).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
