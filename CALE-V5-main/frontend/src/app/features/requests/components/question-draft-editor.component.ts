import { Component, Input, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiImagePickerComponent } from '../../../shared/ui/ui-image-picker.component';
import { CurriculumChoice, CurriculumPickerComponent } from '../../../shared/curriculum/curriculum-picker.component';
import { QUESTION_TYPE_TF, QuestionDraft, RequestsApi } from '../api/requests.api';

/**
 * Question editor (same flow as the instructor's): type, statement + image,
 * answers A/B/C/D or True/False with one correct, and explanation.
 * Edits the given draft in place.
 */
@Component({
  selector: 'app-question-draft-editor',
  standalone: true,
  imports: [FormsModule, UiButtonComponent, UiErrorComponent, UiImagePickerComponent, CurriculumPickerComponent],
  styles: [`
    :host { display: grid; gap: 1rem; }
    .block { display: grid; gap: 0.6rem; }
    .block-title { margin: 0; font-size: var(--text-base); font-weight: 800; }
    .hint { margin: 0; color: var(--color-text-secondary); font-size: var(--text-sm); line-height: 1.45; }
    .types { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.5rem; }
    .type-btn {
      display: grid;
      gap: 0.15rem;
      padding: 0.75rem 0.9rem;
      text-align: left;
      border-radius: var(--radius-md);
      border: 2px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
      cursor: pointer;
    }
    .type-btn small { color: var(--color-text-secondary); }
    .type-btn.on { border-color: var(--color-primary); background: color-mix(in srgb, var(--color-primary) 10%, var(--color-surface)); }
    textarea { min-height: 5.5rem; }
    .answer {
      display: grid;
      grid-template-columns: auto 1fr auto;
      gap: 0.75rem;
      align-items: start;
      padding: 0.8rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      background: var(--color-surface-raised);
    }
    .answer.correct { border-color: var(--color-success); background: var(--color-success-soft); }
    .letter {
      width: 2.6rem;
      height: 2.6rem;
      border-radius: var(--radius-sm);
      border: 2px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      font-weight: 800;
      font-size: 1rem;
      cursor: pointer;
    }
    .letter.on { border-color: var(--color-success); background: var(--color-success); color: #fff; }
    .answer-body { display: grid; gap: 0.5rem; min-width: 0; }
    .answer-label { font-size: var(--text-sm); font-weight: 700; }
    .answer-label.is-correct { color: var(--color-success); }
    .answer-body input {
      width: 100%;
      min-height: 2.6rem;
      padding: 0.55rem 0.75rem;
      border-radius: var(--radius-sm);
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
    }
    .answer-body input:focus { outline: 2px solid var(--color-primary); outline-offset: 1px; }
    .answer-body input[readonly] { background: var(--color-surface-raised); font-weight: 700; }
    @media (max-width: 600px) {
      .answer { grid-template-columns: auto 1fr; }
      .answer .remove { grid-column: 1 / -1; }
    }
  `],
  template: `
    @if (error()) { <ui-error [message]="error()" /> }

    <section class="block">
      <h3 class="block-title">Tipo de pregunta</h3>
      <div class="types" role="radiogroup" aria-label="Tipo de pregunta">
        <button type="button" class="type-btn" [class.on]="!isTrueFalse" role="radio"
          [attr.aria-checked]="!isTrueFalse" (click)="setType('Seleccion multiple')">
          <strong>A · B · C · D</strong>
          <small>Selección múltiple</small>
        </button>
        <button type="button" class="type-btn" [class.on]="isTrueFalse" role="radio"
          [attr.aria-checked]="isTrueFalse" (click)="setType('Verdadero/Falso')">
          <strong>V · F</strong>
          <small>Verdadero o falso</small>
        </button>
      </div>
    </section>

    <section class="block">
      <h3 class="block-title">Enunciado</h3>
      <label class="field">¿Qué debe responder el estudiante?
        <textarea [(ngModel)]="draft.text" name="qd-text" maxlength="2000"
          placeholder="Ej. ¿Qué indica una señal reglamentaria de color rojo con un círculo?"></textarea>
      </label>
      <p class="hint">Imagen del enunciado (opcional): una señal, foto o esquema.</p>
      <ui-image-picker
        [src]="draft.imageUrl || null"
        [busy]="uploading() === 'question'"
        alt="Imagen de la pregunta"
        (selected)="uploadQuestionImage($event)"
        (cleared)="draft.imageUrl = ''" />
    </section>

    <section class="block">
      <h3 class="block-title">Respuestas</h3>
      <p class="hint">Toca la letra para marcar la respuesta correcta. Cada respuesta puede tener texto, imagen o ambos.</p>
      @for (opt of draft.options; track $index) {
        <article class="answer" [class.correct]="opt.isCorrect">
          <button type="button" class="letter" [class.on]="opt.isCorrect" (click)="markCorrect($index)"
            [attr.aria-label]="'Marcar ' + letter($index) + ' como correcta'">{{ letter($index) }}</button>
          <div class="answer-body">
            <span class="answer-label" [class.is-correct]="opt.isCorrect">
              {{ opt.isCorrect ? '✓ Respuesta correcta' : 'Opción ' + letter($index) }}
            </span>
            <input [(ngModel)]="opt.text" [name]="'qd-opt' + $index" maxlength="500"
              [readonly]="isTrueFalse"
              [placeholder]="'Texto de la respuesta ' + letter($index)" />
            @if (!isTrueFalse) {
              <ui-image-picker
                [src]="opt.imageUrl || null"
                [busy]="uploading() === ('opt-' + $index)"
                [alt]="'Imagen de la opción ' + letter($index)"
                (selected)="uploadOptionImage($index, $event)"
                (cleared)="opt.imageUrl = ''" />
            }
          </div>
          @if (!isTrueFalse) {
            <ui-button class="remove" type="button" variant="ghost" [disabled]="draft.options.length <= 2"
              (click)="removeOption($index)">Quitar</ui-button>
          }
        </article>
      }
      @if (!isTrueFalse && draft.options.length < 6) {
        <div>
          <ui-button type="button" variant="secondary" (click)="addOption()">Añadir otra respuesta</ui-button>
        </div>
      }
    </section>

    <section class="block">
      <h3 class="block-title">Malla y explicación (opcional)</h3>
      <app-curriculum-picker
        [subject]="draft.subject"
        [topic]="draft.topic"
        [subtopic]="draft.subtopic"
        (changed)="onCurriculum($event)" />
      <label class="field">¿Por qué esa es la respuesta correcta?
        <textarea [(ngModel)]="draft.explanation" name="qd-expl" maxlength="2000"
          placeholder="Si sabes el artículo o la norma, escríbelo aquí."></textarea>
      </label>
    </section>
  `
})
export class QuestionDraftEditorComponent {
  private readonly api = inject(RequestsApi);

  @Input({ required: true }) draft!: QuestionDraft;

  onCurriculum(choice: CurriculumChoice): void {
    if (choice.subtopic) {
      this.draft.subject = choice.subject;
      this.draft.topic = choice.topic;
      this.draft.subtopic = choice.subtopic;
      return;
    }
    this.draft.subtopic = '';
  }

  readonly uploading = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  get isTrueFalse(): boolean {
    return this.draft.type === QUESTION_TYPE_TF;
  }

  letter(index: number): string {
    return this.isTrueFalse ? (index === 0 ? 'V' : 'F') : String.fromCharCode(65 + index);
  }

  setType(type: string): void {
    if (this.draft.type === type) {
      return;
    }
    this.draft.type = type;
    this.draft.options = type === QUESTION_TYPE_TF
      ? [
          { text: 'Verdadero', isCorrect: true, imageUrl: '' },
          { text: 'Falso', isCorrect: false, imageUrl: '' }
        ]
      : [
          { text: '', isCorrect: true, imageUrl: '' },
          { text: '', isCorrect: false, imageUrl: '' },
          { text: '', isCorrect: false, imageUrl: '' },
          { text: '', isCorrect: false, imageUrl: '' }
        ];
  }

  markCorrect(index: number): void {
    this.draft.options = this.draft.options.map((o, i) => ({ ...o, isCorrect: i === index }));
  }

  addOption(): void {
    if (this.isTrueFalse || this.draft.options.length >= 6) {
      return;
    }
    this.draft.options = [...this.draft.options, { text: '', isCorrect: false, imageUrl: '' }];
  }

  removeOption(index: number): void {
    if (this.draft.options.length <= 2) {
      return;
    }
    const wasCorrect = this.draft.options[index].isCorrect;
    this.draft.options = this.draft.options.filter((_, i) => i !== index);
    if (wasCorrect) {
      this.markCorrect(0);
    }
  }

  uploadQuestionImage(file: File): void {
    this.upload(file, 'question', (url) => (this.draft.imageUrl = url));
  }

  uploadOptionImage(index: number, file: File): void {
    this.upload(file, `opt-${index}`, (url) => {
      this.draft.options = this.draft.options.map((o, i) => (i === index ? { ...o, imageUrl: url } : o));
    });
  }

  private upload(file: File, key: string, apply: (url: string) => void): void {
    this.error.set(null);
    this.uploading.set(key);
    this.api.upload(file).subscribe({
      next: (res) => {
        apply(res.url);
        this.uploading.set(null);
      },
      error: (err: unknown) => {
        this.uploading.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }
}
