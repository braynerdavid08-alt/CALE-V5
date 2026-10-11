import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../core/http/map-api-error';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../shared/ui/ui-page-header.component';
import { Pqr, PqrApi } from './pqr.api';
import { PqrTimelineComponent } from './pqr-timeline.component';

@Component({
  selector: 'app-my-pqr-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiCardComponent, UiErrorComponent, UiPageHeaderComponent, PqrTimelineComponent],
  template: `
    <ui-page-header
      eyebrow="Tu cuenta"
      title="PQR y mis datos"
      subtitle="Peticiones, quejas, reclamos y solicitudes sobre tus datos personales. Nada se borra: cada respuesta queda registrada." />

    <ui-card>
      <h2>¿Qué necesitas?</h2>
      <div class="shortcuts">
        <ui-button variant="secondary" routerLink="/pqr" [queryParams]="{ tipo: 'consulta' }">Saber qué datos tienen de mí</ui-button>
        <ui-button variant="secondary" routerLink="/pqr" [queryParams]="{ tipo: 'rectificacion' }">Corregir mis datos</ui-button>
        <ui-button variant="secondary" routerLink="/pqr" [queryParams]="{ tipo: 'supresion' }">Eliminar mis datos</ui-button>
        <ui-button variant="secondary" routerLink="/pqr" [queryParams]="{ tipo: 'revocatoria' }">Revocar mi autorización</ui-button>
        <ui-button routerLink="/pqr">Otra petición, queja o reclamo</ui-button>
      </div>
    </ui-card>

    <ui-error [message]="error()" />
    <ui-card>
      <h2>Mis solicitudes</h2>
      @for (p of items(); track p.id) {
        <div class="item"><legal-pqr-timeline [pqr]="p" /></div>
      } @empty {
        <p class="muted">Aún no has enviado solicitudes.</p>
      }
    </ui-card>
  `,
  styles: [`
    h2 { margin: 0 0 0.7rem; font-size: 1.05rem; }
    .shortcuts { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .item { padding: 0.85rem 0; border-top: 1px solid var(--color-border); }
    .item:first-of-type { border-top: 0; padding-top: 0; }
    .muted { color: var(--color-text-secondary); margin: 0; }
    ui-card { display: block; margin-bottom: 1rem; }
  `]
})
export class MyPqrPage implements OnInit {
  private readonly api = inject(PqrApi);
  readonly items = signal<Pqr[]>([]);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.mine().subscribe({
      next: (rows) => this.items.set(rows),
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
