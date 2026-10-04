import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { CurriculumAdminApi, CurriculumLessonPlan, CurriculumPlan } from '../api/courses.api';

const ACTIONS: Record<CurriculumLessonPlan['action'], string> = {
  create: 'Nueva',
  update: 'Se actualiza',
  unchanged: 'Sin cambios',
  remove: 'Se integra y desaparece',
  'keep-edited': 'Editada a mano: se conserva',
  'keep-unknown': 'Fuera de la malla: se conserva'
};

const COURSE_ACTIONS: Record<string, string> = {
  create: 'Curso nuevo',
  update: 'Cambia',
  unchanged: 'Sin cambios',
  'skip-inactive': 'Inactivo: no se toca'
};

/** Admin review of how the platform courses differ from the curriculum, and the explicit action that applies it. */
@Component({
  selector: 'app-curriculum-sync-panel',
  standalone: true,
  imports: [FormsModule, UiButtonComponent],
  styleUrl: '../pages/courses.css',
  styles: [`
    .summary { display: flex; flex-wrap: wrap; gap: 0.4rem 1.2rem; margin: 0.6rem 0; }
    .summary strong { font-size: var(--text-lg); }
    details.course { border-top: 1px solid var(--border, #e5e7eb); padding: 0.5rem 0; }
    details.course summary { cursor: pointer; font-weight: 600; }
    .rows { list-style: none; margin: 0.4rem 0 0; padding: 0; display: grid; gap: 0.35rem; }
    .rows li { display: grid; gap: 0.15rem; padding: 0.4rem 0.5rem; border-radius: 0.5rem; background: var(--surface-2, rgba(0,0,0,0.03)); }
    .rows li.remove { opacity: 0.75; }
    .rows .head { display: flex; flex-wrap: wrap; gap: 0.4rem; align-items: baseline; }
    .rows .pos { font-variant-numeric: tabular-nums; min-width: 4.5rem; }
    .manual { margin: 0.2rem 0 0 1rem; padding: 0; font-size: var(--text-sm); }
    .apply { display: flex; flex-wrap: wrap; gap: 0.8rem; align-items: center; margin-top: 0.8rem; }
  `],
  template: `
    <section class="panel">
      <h2>Malla curricular de la plataforma</h2>
      <p class="muted" style="margin: 0">
        Compara los cursos de Luz Verde con la malla actual. Revisar no cambia nada; las lecciones editadas a mano
        y los cursos de las escuelas nunca se modifican.
      </p>

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }
      @if (done()) {
        <p class="ok-msg" role="status">{{ done() }}</p>
      }

      <div class="card-actions">
        <ui-button variant="secondary" [loading]="loading()" (click)="review()">Revisar cambios</ui-button>
      </div>

      @if (plan(); as p) {
        <div class="summary">
          <span>Lecciones hoy: <strong>{{ p.lessonsBefore }}</strong></span>
          <span>Lecciones después: <strong>{{ p.lessonsAfter }}</strong></span>
          <span>Malla oficial: <strong>{{ p.seedLessons }}</strong></span>
          <span>Registros de progreso: <strong>{{ p.progressRows }}</strong></span>
          <span>Cursos de escuelas (no se tocan): <strong>{{ p.schoolCoursesUntouched }}</strong></span>
        </div>

        @for (c of p.courses; track c.slug) {
          <details class="course" [open]="c.action !== 'unchanged'">
            <summary>
              {{ c.title }} · {{ c.lessonsBefore }} → {{ c.lessonsAfter }}
              <span class="tag" [class.gray]="c.action === 'unchanged'">{{ courseAction(c.action) }}</span>
              @if (c.hasEditedLessons) {
                <span class="tag">Tiene ediciones manuales</span>
              }
            </summary>
            <ul class="rows">
              @for (l of c.lessons; track $index) {
                <li [class.remove]="l.action === 'remove'">
                  <div class="head">
                    <span class="pos muted">{{ l.positionBefore ?? '—' }} → {{ l.positionAfter ?? '—' }}</span>
                    <strong>{{ l.title }}</strong>
                    <span class="tag" [class.gray]="l.action === 'unchanged'">{{ action(l.action) }}</span>
                    @if (l.progress) {
                      <small class="muted">{{ l.progress }} con progreso</small>
                    }
                  </div>
                  @if (l.detail) {
                    <small class="muted">{{ l.detail }}</small>
                  }
                  @if (l.action === 'keep-edited') {
                    <small class="muted">
                      Preguntas iguales a la malla: {{ l.questionsLikeSeed }} · preguntas de la malla que no tiene: {{ l.seedQuestionsMissing }}
                    </small>
                    @if (l.manualQuestions.length) {
                      <small>Preguntas propias o modificadas ({{ l.manualQuestions.length }}):</small>
                      <ul class="manual">
                        @for (q of l.manualQuestions; track $index) {
                          <li>{{ q }}</li>
                        }
                      </ul>
                    }
                  }
                </li>
              }
            </ul>
          </details>
        }

        @if (!p.applied) {
          <div class="apply">
            <label>
              <input type="checkbox" [(ngModel)]="resetProgress" />
              Reiniciar el progreso de todos los cursos de Luz Verde ({{ p.progressRows }} registros)
            </label>
            <ui-button [loading]="applying()" (click)="apply(p)">Aplicar la malla</ui-button>
          </div>
        }
      }
    </section>
  `
})
export class CurriculumSyncPanelComponent {
  private readonly api = inject(CurriculumAdminApi);

  readonly loading = signal(false);
  readonly applying = signal(false);
  readonly error = signal<string | null>(null);
  readonly done = signal<string | null>(null);
  readonly plan = signal<CurriculumPlan | null>(null);
  resetProgress = false;

  action(a: CurriculumLessonPlan['action']): string {
    return ACTIONS[a] ?? a;
  }

  courseAction(a: string): string {
    return COURSE_ACTIONS[a] ?? a;
  }

  review(): void {
    this.loading.set(true);
    this.error.set(null);
    this.done.set(null);
    this.api.plan().subscribe({
      next: (p) => {
        this.plan.set(p);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  apply(p: CurriculumPlan): void {
    const reset = this.resetProgress
      ? `\n\nTambién se borrarán ${p.progressRows} registros de progreso: todos los cursos de Luz Verde vuelven a 0 %.`
      : '';
    if (!confirm(`Se aplicará la malla: ${p.lessonsBefore} → ${p.lessonsAfter} lecciones.${reset}\n\n¿Continuar?`)) return;
    this.applying.set(true);
    this.error.set(null);
    this.api.apply(this.resetProgress).subscribe({
      next: (result) => {
        this.plan.set(result);
        this.applying.set(false);
        this.done.set(`Malla aplicada: ${result.lessonsAfter} lecciones. Progreso borrado: ${result.progressRowsRemoved} registros.`);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.applying.set(false);
      }
    });
  }
}
