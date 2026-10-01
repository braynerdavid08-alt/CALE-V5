import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { ReportQuestionComponent } from '../../requests/components/report-question.component';
import { AnswerFeedback, PlayQuestion } from '../api/play.api';

@Component({
  selector: 'play-question',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReportQuestionComponent],
  template: `
    <article class="pq">
      @if (question.topic) {
        <p class="pq-topic">{{ question.topic }}</p>
      }
      <h2 class="pq-text">{{ question.text }}</h2>
      @if (question.imageUrl) {
        <img class="pq-image" [src]="media(question.imageUrl)" alt="Imagen de la pregunta" />
      }
      <div class="pq-options" role="group" aria-label="Opciones de respuesta">
        @for (option of question.options; track option.id; let i = $index) {
          <button
            type="button"
            class="pq-option"
            [class.is-selected]="selectedId === option.id"
            [class.is-correct]="!!feedback && feedback.correctOptionId === option.id"
            [class.is-wrong]="!!feedback && !feedback.correct && selectedId === option.id"
            [disabled]="!!feedback || busy"
            (click)="answered.emit(option.id)">
            <span class="pq-letter" aria-hidden="true">{{ letters[i] }}</span>
            <span class="pq-option-body">
              @if (option.imageUrl) {
                <img [src]="media(option.imageUrl)" alt="" />
              }
              @if (option.text) {
                <span>{{ option.text }}</span>
              }
            </span>
          </button>
        }
      </div>
      @if (feedback) {
        <div class="pq-feedback" [class.ok]="feedback.correct" role="status">
          <strong>{{ feedback.correct ? '¡Correcto!' : 'No es correcta' }}</strong>
          @if (feedback.explanation) {
            <p>{{ feedback.explanation }}</p>
          } @else if (!feedback.correct) {
            <p>La respuesta correcta quedó marcada en verde.</p>
          }
        </div>
        @if (reportable) {
          <app-report-question [questionId]="question.id" [questionText]="question.text" />
        }
      }
    </article>
  `,
  styles: [`
    .pq { display: grid; gap: 0.9rem; }
    .pq-topic {
      margin: 0;
      font-size: var(--text-xs);
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--color-primary);
    }
    .pq-text { margin: 0; font-size: clamp(1.1rem, 2.4vw, 1.35rem); line-height: 1.35; }
    .pq-image {
      max-width: min(100%, 320px);
      max-height: 240px;
      object-fit: contain;
      justify-self: center;
      border-radius: var(--radius-md);
      background: var(--color-surface-raised);
    }
    .pq-options { display: grid; gap: 0.6rem; }
    .pq-option {
      display: flex;
      align-items: center;
      gap: 0.8rem;
      width: 100%;
      min-height: 3.4rem;
      padding: 0.7rem 0.9rem;
      text-align: left;
      font: inherit;
      font-size: var(--text-md);
      color: var(--color-text);
      background: var(--color-surface);
      border: 2px solid var(--color-border);
      border-radius: var(--radius-md);
      cursor: pointer;
      transition: border-color 0.15s ease, background 0.15s ease, transform 0.1s ease;
    }
    .pq-option:hover:not(:disabled) { border-color: var(--color-primary); transform: translateY(-1px); }
    .pq-option:disabled { cursor: default; }
    .pq-option.is-selected { border-color: var(--color-primary); }
    .pq-option.is-correct {
      border-color: var(--color-success);
      background: var(--color-success-soft);
      animation: pq-pop 0.35s ease;
    }
    .pq-option.is-wrong {
      border-color: var(--color-danger);
      background: var(--color-danger-soft);
      animation: pq-shake 0.35s ease;
    }
    .pq-letter {
      flex: none;
      display: grid;
      place-items: center;
      width: 2rem;
      height: 2rem;
      border-radius: 50%;
      font-weight: 700;
      background: var(--color-chip);
    }
    .pq-option-body { display: flex; align-items: center; gap: 0.6rem; flex-wrap: wrap; }
    .pq-option-body img { max-height: 72px; max-width: 120px; object-fit: contain; }
    .pq-feedback {
      padding: 0.8rem 1rem;
      border-radius: var(--radius-md);
      background: var(--color-danger-soft);
      border: 1px solid var(--color-danger);
    }
    .pq-feedback.ok { background: var(--color-success-soft); border-color: var(--color-success); }
    .pq-feedback p { margin: 0.35rem 0 0; line-height: 1.45; }
    @keyframes pq-pop { 50% { transform: scale(1.03); } }
    @keyframes pq-shake {
      25% { transform: translateX(-6px); }
      75% { transform: translateX(6px); }
    }
    @media (prefers-reduced-motion: reduce) {
      .pq-option { transition: none; }
      .pq-option.is-correct, .pq-option.is-wrong { animation: none; }
    }
  `]
})
export class PlayQuestionComponent {
  @Input({ required: true }) question!: PlayQuestion;
  @Input() feedback: AnswerFeedback | null = null;
  @Input() selectedId: number | null = null;
  @Input() busy = false;
  /** Show "Reportar pregunta" after answering (off in timed games like the duel). */
  @Input() reportable = true;
  @Output() readonly answered = new EventEmitter<number>();

  readonly letters = ['A', 'B', 'C', 'D', 'E', 'F'];
  readonly media = resolveMediaUrl;
}
