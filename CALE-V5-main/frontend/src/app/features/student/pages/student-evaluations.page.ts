import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { ExamApi, ReviewResponse } from '../api/exam.api';
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
                <strong>{{ modeLabel(r.mode) }}</strong>
                <p class="meta">{{ formatDate(r.finishedAt) }}</p>
              </div>
              <div class="right">
                <span class="score">{{ r.percent }}%</span>
                <ui-badge [tone]="r.passed ? 'success' : 'danger'">
                  {{ r.passed ? 'Aprobado' : 'No aprobado' }}
                </ui-badge>
                <ui-button
                  type="button"
                  variant="ghost"
                  [disabled]="reviewLoading() === r.attemptId"
                  (click)="openReview(r.attemptId)">
                  Ver revisión
                </ui-button>
              </div>
            </li>
          }
        </ul>
      </section>
    }

    @if (review(); as rev) {
      <section class="panel review">
        <div class="review-head">
          <h2>Revisión del intento</h2>
          <ui-button type="button" variant="ghost" (click)="closeReview()">Cerrar</ui-button>
        </div>
        <p class="meta">
          {{ rev.result.correctCount }} / {{ rev.result.totalQuestions }} correctas ·
          {{ rev.result.percent }}%
        </p>
        <ol class="review-list">
          @for (q of rev.questions; track q.id) {
            <li [class.ok]="q.isCorrect" [class.bad]="!q.isCorrect">
              <strong>{{ q.order }}. {{ q.text }}</strong>
              <ul>
                @for (o of q.options; track o.id) {
                  <li>
                    @if (o.selected) { → }
                    {{ o.text }}
                    @if (o.isCorrect) { (correcta) }
                  </li>
                }
              </ul>
              @if (q.explanation) {
                <p class="meta">{{ q.explanation }}</p>
              }
            </li>
          }
        </ol>
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
      margin-bottom: 1rem;
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
      flex-wrap: wrap;
    }
    .list li + li { border-top: 1px solid var(--color-border); }
    .meta { margin: 0.2rem 0 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .right { display: flex; align-items: center; gap: 0.65rem; flex-wrap: wrap; }
    .score { font-weight: 700; }
    .review-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 1rem;
    }
    .review-list { margin: 0.75rem 0 0; padding-left: 1.1rem; display: grid; gap: 0.85rem; }
    .review-list li.ok { color: var(--color-success, inherit); }
    .review-list li.bad { color: var(--color-danger, inherit); }
    .review-list ul { margin: 0.35rem 0 0; padding-left: 1rem; color: var(--color-text); }
  `]
})
export class StudentEvaluationsPage implements OnInit {
  private readonly api = inject(StudentApi);
  private readonly examApi = inject(ExamApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly results = signal<ResultRow[]>([]);
  readonly review = signal<ReviewResponse | null>(null);
  readonly reviewLoading = signal<number | null>(null);

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

  modeLabel(mode: string): string {
    const key = (mode || '').toLowerCase();
    if (key === 'exam' || key === 'examen') return 'Examen';
    if (key === 'practice' || key === 'practica' || key === 'práctica') return 'Práctica';
    return mode || 'Evaluación';
  }

  openReview(attemptId: number): void {
    this.error.set(null);
    this.reviewLoading.set(attemptId);
    this.examApi.review(attemptId).subscribe({
      next: (rev) => {
        this.review.set(rev);
        this.reviewLoading.set(null);
      },
      error: (err) => {
        this.reviewLoading.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  closeReview(): void {
    this.review.set(null);
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
