import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { PlayApi, Readiness, ReadinessTopic } from '../api/play.api';
import { PlayTopbarComponent } from '../components/play-topbar.component';

const LEVEL_LABELS: Record<ReadinessTopic['level'], string> = {
  alto: 'Dominado',
  medio: 'En progreso',
  bajo: 'Por reforzar',
  sin_datos: 'Sin práctica'
};

@Component({
  selector: 'app-readiness-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiLoadingComponent, PlayTopbarComponent],
  styleUrl: './play-page.css',
  styles: [`
    .gauge-wrap { display: grid; justify-items: center; gap: 0.75rem; text-align: center; }
    .gauge {
      --value: 0;
      --tone: var(--color-danger);
      width: min(15rem, 70vw);
      aspect-ratio: 1;
      border-radius: 50%;
      display: grid;
      place-items: center;
      background: conic-gradient(var(--tone) calc(var(--value) * 1%), var(--color-chip) 0);
    }
    .gauge-inner {
      width: 78%;
      aspect-ratio: 1;
      border-radius: 50%;
      display: grid;
      place-content: center;
      background: var(--color-surface);
    }
    .gauge-inner strong { font-size: clamp(2.4rem, 8vw, 3.2rem); line-height: 1; }
    .gauge-inner span { color: var(--color-text-secondary); font-size: var(--text-sm); }
    .label { margin: 0; font-size: var(--text-xl); font-weight: 800; }
    .reco { margin: 0; max-width: 34rem; color: var(--color-text-secondary); line-height: 1.5; }
    .facts { display: flex; flex-wrap: wrap; gap: 0.5rem; justify-content: center; }
    .topics { display: grid; gap: 0.85rem; }
    .topic { display: grid; gap: 0.35rem; }
    .topic-head { display: flex; justify-content: space-between; gap: 0.5rem; font-weight: 600; }
    .topic-head small { font-weight: 500; color: var(--color-text-secondary); }
    .topic-bar { height: 0.65rem; border-radius: 999px; background: var(--color-chip); overflow: hidden; }
    .topic-bar span { display: block; height: 100%; border-radius: inherit; }
    .lvl-alto span { background: var(--color-success); }
    .lvl-medio span { background: #f59e0b; }
    .lvl-bajo span { background: var(--color-danger); }
    .lvl-sin_datos span { background: var(--color-border-strong); }
    h2 { margin: 0 0 0.85rem; font-size: var(--text-lg); }
    .study-lede { margin: -0.5rem 0 0.85rem; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .study { display: grid; gap: 0.6rem; }
    .study-item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      padding: 0.75rem 0.9rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      color: inherit;
      text-decoration: none;
    }
    .study-item:hover { border-color: var(--color-primary); }
    .study-body { display: grid; gap: 0.15rem; }
    .study-body small { color: var(--color-text-secondary); }
  `],
  template: `
    <section class="play-page">
      <play-topbar
        title="¿Estoy listo para el examen?"
        subtitle="Calculado con tus últimos simulacros y tus respuestas por tema." />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (loading()) {
        <ui-loading />
      } @else {
        @if (data(); as d) {
        <div class="play-card gauge-wrap">
          <div class="gauge" [style.--value]="d.overall" [style.--tone]="tone()">
            <div class="gauge-inner">
              <strong>{{ d.overall }}%</strong>
              <span>preparación</span>
            </div>
          </div>
          <p class="label">{{ d.label }}</p>
          <p class="reco">{{ d.recommendation }}</p>
          <div class="facts">
            <span class="chip">📝 Promedio reciente: {{ d.recentAttemptsAverage }}%</span>
            <span class="chip">❓ Preguntas respondidas: {{ d.answeredQuestions }}</span>
          </div>
          <div class="play-actions center">
            <ui-button routerLink="/student/simulator">Hacer un simulacro</ui-button>
            <ui-button routerLink="/student/play/mistakes" variant="secondary">Repasar errores</ui-button>
          </div>
        </div>

        @if (d.studyLessons.length) {
          <div class="play-card">
            <h2>Lecciones para repasar</h2>
            <p class="study-lede">Según las preguntas que más fallas en los simulacros.</p>
            <div class="study">
              @for (s of d.studyLessons; track s.lesson.lessonId) {
                <a class="study-item" [routerLink]="['/student/cursos/leccion', s.lesson.lessonId]">
                  <span class="study-body">
                    <strong>{{ s.lesson.lessonTitle }}</strong>
                    <small>{{ s.lesson.courseTitle }}</small>
                    <small>Fallaste {{ s.wrong }} de {{ s.answered }} preguntas de este tema · {{ s.percent }}% de aciertos</small>
                  </span>
                  <span class="chip">{{ s.lesson.completed ? 'Volver a verla' : 'Ver lección' }}</span>
                </a>
              }
            </div>
          </div>
        }

        @if (d.topics.length) {
          <div class="play-card">
            <h2>Por tema</h2>
            <div class="topics">
              @for (t of d.topics; track t.blockId) {
                <div [class]="'topic lvl-' + t.level">
                  <div class="topic-head">
                    <span>{{ t.name }}</span>
                    <small>
                      {{ levelLabel(t.level) }}
                      @if (t.answered) { · {{ t.percent }}% ({{ t.correct }}/{{ t.answered }}) }
                      @if (t.lowData && t.answered) { · pocas respuestas }
                    </small>
                  </div>
                  <div class="topic-bar"><span [style.width.%]="t.answered ? t.percent : 4"></span></div>
                </div>
              }
            </div>
          </div>
        }
        }
      }
    </section>
  `
})
export class ReadinessPage implements OnInit {
  private readonly api = inject(PlayApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly data = signal<Readiness | null>(null);

  readonly tone = computed(() => {
    const v = this.data()?.overall ?? 0;
    if (v >= 80) return 'var(--color-success)';
    if (v >= 60) return '#f59e0b';
    return 'var(--color-danger)';
  });

  ngOnInit(): void {
    this.api.readiness().subscribe({
      next: (d) => {
        this.data.set(d);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  levelLabel(level: ReadinessTopic['level']): string {
    return LEVEL_LABELS[level] ?? level;
  }
}
