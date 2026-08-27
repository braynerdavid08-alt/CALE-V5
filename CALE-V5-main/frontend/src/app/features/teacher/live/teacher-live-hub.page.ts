import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { LiveApi } from '../../live/api/live.api';

@Component({
  selector: 'app-teacher-live-hub-page',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, UiButtonComponent, UiErrorComponent],
  template: `
    <section class="page">
      <header class="hero">
        <p class="eyebrow">CALE LIVE</p>
        <h1>Aula en Vivo</h1>
        <p class="lead">
          Proyecta simulacros del banco CALE. Los estudiantes entran con QR o código desde el celular.
        </p>
      </header>

      <ui-error [message]="error()" />

      <div class="modes">
        <article class="card active">
          <h2>Simular examen CALE</h2>
          <p>Usa el banco de preguntas existente. Preset Estándar CALE (25 preguntas) o configuración libre.</p>
          <form [formGroup]="form" (ngSubmit)="create()">
            <label class="field">
              Título de la actividad
              <input formControlName="title" />
            </label>
            <label class="field">
              Modo
              <select formControlName="mode">
                <option value="Exam">Examen (sin revelar durante)</option>
                <option value="Competitive">Competitivo</option>
                <option value="Pedagogical">Pedagógico</option>
              </select>
            </label>
            <label class="check">
              <input type="checkbox" formControlName="caleStandardPreset" />
              Simular examen CALE completo (25 preguntas del banco de normas)
            </label>
            @if (!form.controls.caleStandardPreset.value) {
              <div class="grid">
                <label class="field">
                  Cantidad
                  <input type="number" formControlName="questionCount" min="1" max="100" />
                </label>
                <label class="field">
                  Segundos por pregunta
                  <input type="number" formControlName="secondsPerQuestion" min="5" max="600" />
                </label>
              </div>
            }
            <ui-button type="submit" [loading]="loading()">Crear sala y proyectar</ui-button>
          </form>
        </article>

        <article class="card muted">
          <h2>Quiz en vivo</h2>
          <p>Próximamente (Fase B3+).</p>
        </article>
        <article class="card muted">
          <h2>Pregunta rápida</h2>
          <p>Próximamente.</p>
        </article>
        <article class="card muted">
          <h2>Revancha / Resultados</h2>
          <p>Próximamente (Fases B4–B5).</p>
        </article>
      </div>

      <p class="hint">
        ¿Los estudiantes ya tienen el código?
        <a routerLink="/live/join">Ir a unirse</a>
      </p>
    </section>
  `,
  styles: `
    .page { padding: var(--page-pad); max-width: 960px; margin: 0 auto; }
    .eyebrow { color: var(--color-primary); font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; font-size: var(--text-xs); }
    .lead { color: var(--color-text-secondary); }
    .modes { display: grid; gap: 1rem; margin-top: 1.25rem; }
    .card { border: 1px solid var(--color-border); border-radius: var(--radius-lg); padding: 1.1rem; background: var(--color-surface); }
    .card.muted { opacity: 0.65; }
    .field { display: grid; gap: 0.35rem; margin: 0.75rem 0; font-size: var(--text-sm); }
    .field input, .field select { padding: 0.65rem 0.8rem; border-radius: var(--radius-md); border: 1px solid var(--color-border); background: var(--color-background); color: var(--color-text); }
    .check { display: flex; gap: 0.5rem; align-items: flex-start; font-size: var(--text-sm); margin: 0.75rem 0; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .hint { margin-top: 1.5rem; color: var(--color-text-secondary); }
    @media (max-width: 640px) { .grid { grid-template-columns: 1fr; } }
  `
})
export class TeacherLiveHubPage {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(LiveApi);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    title: ['CALE Aula en Vivo'],
    mode: ['Exam', Validators.required],
    caleStandardPreset: [true],
    questionCount: [10],
    secondsPerQuestion: [30]
  });

  create(): void {
    this.error.set(null);
    this.loading.set(true);
    const v = this.form.getRawValue();
    this.api.create({
      title: v.title,
      mode: v.mode,
      config: {
        caleStandardPreset: v.caleStandardPreset,
        questionCount: v.questionCount,
        secondsPerQuestion: v.secondsPerQuestion,
        randomize: true,
        shuffleOptions: true,
        showRanking: v.mode === 'Competitive',
        anonymousNames: false,
        feedbackTiming: v.mode === 'Exam' ? 'end' : 'immediate'
      }
    }).subscribe({
      next: (lobby) => {
        this.loading.set(false);
        void this.router.navigate(['/teacher/live', lobby.sessionId, 'host']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
