import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';

@Component({
  selector: 'app-student-placeholder-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiCardComponent, UiPageHeaderComponent],
  template: `
    <ui-page-header
      [eyebrow]="eyebrow"
      [title]="title"
      [subtitle]="subtitle" />

    <ui-card>
      <p class="lead">
        Esta sección del panel de estudiante está reservada. El menú ya está listo;
        la funcionalidad se completará en una siguiente iteración.
      </p>
      <ul class="notes">
        @for (note of notes; track note) {
          <li>{{ note }}</li>
        }
      </ul>
      <div class="row">
        <a routerLink="/student">
          <ui-button type="button" variant="secondary">Volver al Dashboard</ui-button>
        </a>
      </div>
    </ui-card>
  `,
  styles: [`
    .lead { margin: 0 0 0.85rem; color: var(--color-text-secondary); line-height: 1.5; }
    .notes {
      margin: 0 0 1.25rem;
      padding-left: 1.15rem;
      color: var(--color-text);
      line-height: 1.55;
    }
    .row { display: flex; gap: 0.65rem; flex-wrap: wrap; }
  `]
})
export class StudentPlaceholderPage {
  private readonly route = inject(ActivatedRoute);
  private readonly data = this.route.snapshot.data;

  readonly title = String(this.data['title'] ?? 'Módulo en construcción');
  readonly subtitle = String(
    this.data['subtitle'] ?? 'Esta sección formará parte del panel de estudiante.'
  );
  readonly eyebrow = String(this.data['eyebrow'] ?? 'Estudiante');
  readonly notes: string[] = Array.isArray(this.data['notes'])
    ? (this.data['notes'] as string[])
    : [
        'La opción ya aparece en el menú lateral.',
        'Puedes entrar y salir sin errores mientras se define el alcance.'
      ];
}
