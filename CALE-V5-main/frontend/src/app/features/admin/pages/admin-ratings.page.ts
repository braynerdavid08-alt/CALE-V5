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

interface RatingRow {
  id: number;
  userName: string;
  stars: number;
  comment?: string | null;
}

@Component({
  selector: 'app-admin-ratings-page',
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
      title="Actividad"
      subtitle="Valoraciones reales de intentos." />
    <ui-error [message]="error()" />
    @if (loading()) {
      <ui-loading />
    } @else if (!items().length) {
      <ui-empty title="No hay valoraciones" message="Cuando los estudiantes valoren un intento aparecerán aquí." />
    } @else {
      <ui-card>
        <div class="table-wrap">
          <table class="data">
            <thead>
              <tr>
                <th>Usuario</th>
                <th>Estrellas</th>
                <th>Comentario</th>
              </tr>
            </thead>
            <tbody>
              @for (item of items(); track item.id) {
                <tr>
                  <td>{{ item.userName }}</td>
                  <td><ui-badge tone="warning">{{ item.stars }}/5</ui-badge></td>
                  <td>{{ item.comment || '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </ui-card>
    }
  `
})
export class AdminRatingsPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly items = signal<RatingRow[]>([]);
  readonly error = signal<string | null>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.http.get<RatingRow[]>(`${env.apiUrl}/api/ratings`).subscribe({
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
