import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { StudentApi } from '../api/student.api';

interface ResultRow {
  attemptId: number;
  percent: number;
  passed: boolean;
  mode: string;
  finishedAt?: string | null;
}

@Component({
  selector: 'app-student-evaluations-page',
  standalone: true,
  imports: [
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Estudiante"
      title="Mis evaluaciones"
      subtitle="Resultados de tus intentos y acceso al simulador de práctica." />

    <ui-error [message]="error()" />

    <div class="actions">
      <a routerLink="/student/simulator">
        <ui-button type="button">Abrir simulador</ui-button>
      </a>
    </div>

    @if (loading()) {
      <ui-loading />
    } @else if (!results().length) {
      <ui-empty
        title="Sin evaluaciones"
        message="Cuando completes un intento en el simulador, aparecerá aquí." />
    } @else {
      <section class="panel">
        <div class="stats">
          <div>
            <span class="label">Aprobadas</span>
            <strong>{{ passedCount() }} / {{ results().length }}</strong>
          </div>
          <div>
            <span class="label">Mejor marca</span>
            <strong>{{ bestPercent() }}%</strong>
          </div>
        </div>
        <ul class="list">
          @for (r of results(); track r.attemptId) {
            <li>
              <div>
                <strong>{{ r.mode || 'Evaluación' }}</strong>
                <p class="meta">{{ formatDate(r.finishedAt) }}</p>
              </div>
              <div class="right">
                <span class="score">{{ r.percent }}%</span>
                <ui-badge [tone]="r.passed ? 'success' : 'danger'">
                  {{ r.passed ? 'Aprobado' : 'No aprobado' }}
                </ui-badge>
              </div>
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: [`
    .actions { margin: 0 0 1rem; }
    .panel {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 1.1rem 1.25rem;
    }
    .stats {
      display: flex;
      flex-wrap: wrap;
      gap: 1.5rem;
      margin-bottom: 1rem;
      padding-bottom: 1rem;
      border-bottom: 1px solid var(--color-border);
    }
    .label {
      display: block;
      font-size: var(--text-xs);
      color: var(--color-text-secondary);
      margin-bottom: 0.2rem;
    }
    .list { list-style: none; margin: 0; padding: 0; }
    .list li {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: center;
      padding: 0.85rem 0;
    }
    .list li + li { border-top: 1px solid var(--color-border); }
    .meta { margin: 0.2rem 0 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .right { display: flex; align-items: center; gap: 0.65rem; }
    .score { font-weight: 700; }
  `]
})
export class StudentEvaluationsPage implements OnInit {
  private readonly api = inject(StudentApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly results = signal<ResultRow[]>([]);

  readonly passedCount = computed(
    () => this.results().filter((r) => r.passed).length
  );
  readonly bestPercent = computed(() => {
    const rows = this.results();
    if (!rows.length) return 0;
    return Math.max(...rows.map((r) => Number(r.percent) || 0));
  });

  ngOnInit(): void {
    this.api.results().subscribe({
      next: (rows) => {
        this.results.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  formatDate(value?: string | null): string {
    if (!value) return 'Sin fecha';
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return value;
    return d.toLocaleDateString('es-ES', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }
}
