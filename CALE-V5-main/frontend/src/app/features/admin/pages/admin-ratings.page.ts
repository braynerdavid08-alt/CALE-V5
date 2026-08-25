import { Component, inject, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';

interface RatingRow {
  id: number;
  userName: string;
  stars: number;
  comment?: string | null;
  reviewed: boolean;
  hidden: boolean;
}

@Component({
  selector: 'app-admin-ratings-page',
  standalone: true,
  imports: [
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiSuccessComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Administración"
      title="Valoraciones"
      subtitle="Gestiona comentarios de intentos: revisar u ocultar." />
    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />
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
                <th>Estado</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (item of items(); track item.id) {
                <tr>
                  <td>{{ item.userName }}</td>
                  <td><ui-badge tone="warning">{{ item.stars }}/5</ui-badge></td>
                  <td>{{ item.comment || '—' }}</td>
                  <td>
                    <ui-badge [tone]="item.hidden ? 'danger' : item.reviewed ? 'success' : 'neutral'">
                      {{ item.hidden ? 'Oculta' : item.reviewed ? 'Revisada' : 'Nueva' }}
                    </ui-badge>
                  </td>
                  <td class="row">
                    @if (!item.reviewed) {
                      <ui-button type="button" variant="secondary" (click)="review(item)">
                        Marcar revisada
                      </ui-button>
                    }
                    <ui-button type="button" variant="ghost" (click)="toggleHidden(item)">
                      {{ item.hidden ? 'Mostrar' : 'Ocultar' }}
                    </ui-button>
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
export class AdminRatingsPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly items = signal<RatingRow[]>([]);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
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

  review(item: RatingRow): void {
    this.patch(item.id, { reviewed: true }, 'Valoración marcada como revisada.');
  }

  toggleHidden(item: RatingRow): void {
    this.patch(
      item.id,
      { hidden: !item.hidden },
      item.hidden ? 'Valoración visible otra vez.' : 'Valoración ocultada.'
    );
  }

  private patch(
    id: number,
    body: { reviewed?: boolean; hidden?: boolean },
    message: string
  ): void {
    this.error.set(null);
    this.http.patch<void>(`${env.apiUrl}/api/ratings/${id}`, body).subscribe({
      next: () => {
        this.ok.set(message);
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
