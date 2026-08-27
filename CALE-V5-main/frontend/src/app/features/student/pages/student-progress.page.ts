import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiDashBarsComponent, DashBarItem } from '../../../shared/ui/ui-dash-bars.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { StudentApi } from '../api/student.api';

@Component({
  selector: 'app-student-progress-page',
  standalone: true,
  imports: [
    RouterLink,
    UiButtonComponent,
    UiDashBarsComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Estudiante"
      title="Mi progreso"
      subtitle="Resumen de tu avance en simulador, evaluaciones y aula." />

    <ui-error [message]="error()" />

    @if (loading()) {
      <ui-loading />
    } @else {
      <section class="panel">
        <ui-dash-bars [items]="bars()" />
        <div class="actions">
          <a routerLink="/student/simulator">
            <ui-button type="button">Practicar en simulador</ui-button>
          </a>
          <a routerLink="/student/evaluations">
            <ui-button type="button" variant="secondary">Ver evaluaciones</ui-button>
          </a>
        </div>
      </section>
    }
  `,
  styles: [`
    .panel {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 1.25rem;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.65rem;
      margin-top: 1.25rem;
    }
  `]
})
export class StudentProgressPage implements OnInit {
  private readonly api = inject(StudentApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly bestPercent = signal(0);
  readonly passed = signal(0);
  readonly totalAttempts = signal(0);
  readonly groups = signal(0);
  readonly pending = signal(0);

  readonly bars = computed<DashBarItem[]>(() => {
    const total = Math.max(this.totalAttempts(), 1);
    return [
      {
        label: 'Mejor marca simulador',
        value: Math.round(this.bestPercent()),
        max: 100,
        tone: 'success'
      },
      {
        label: 'Evaluaciones aprobadas',
        value: this.passed(),
        max: total,
        tone: 'primary'
      },
      {
        label: 'Grupos activos',
        value: this.groups(),
        max: Math.max(this.groups(), 3),
        tone: 'info'
      },
      {
        label: 'Actividades pendientes',
        value: this.pending(),
        max: Math.max(this.pending(), 5),
        tone: this.pending() > 0 ? 'warning' : 'success'
      }
    ];
  });

  ngOnInit(): void {
    forkJoin({
      dash: this.api.dashboard(),
      results: this.api.results().pipe(catchError(() => of([])))
    }).subscribe({
      next: ({ dash, results }) => {
        this.bestPercent.set(Number(dash.bestPercent ?? 0));
        this.groups.set(dash.groups.length);
        this.pending.set(dash.pendingActivities.length);
        this.passed.set(results.filter((r) => r.passed).length);
        this.totalAttempts.set(results.length);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
