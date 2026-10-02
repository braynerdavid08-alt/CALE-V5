import { ChangeDetectionStrategy, Component, HostListener, Input, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { RequestsApi } from '../api/requests.api';

const REASONS = [
  'La respuesta marcada como correcta está mal',
  'La imagen no corresponde o no se ve',
  'El texto es confuso o tiene errores',
  'Está desactualizada (norma vieja)',
  'Otro problema'
];

/** "Reportar pregunta" link + sheet; sends a report request to the admin. */
@Component({
  selector: 'app-report-question',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, UiButtonComponent],
  template: `
    @if (sent()) {
      <span class="rq-done" role="status">✓ Gracias, el administrador revisará la pregunta.</span>
    } @else {
      <button type="button" class="rq-link" (click)="openSheet()">🚩 Reportar pregunta</button>
    }

    @if (open()) {
      <div class="rq-overlay" (click)="close()">
        <div class="rq-sheet" role="dialog" aria-modal="true" aria-labelledby="rq-title" (click)="$event.stopPropagation()">
          <h2 id="rq-title">¿Qué está mal en esta pregunta?</h2>
          <p class="rq-q">{{ questionText }}</p>
          <div class="rq-reasons" role="radiogroup" aria-label="Motivo">
            @for (r of reasons; track r) {
              <label class="rq-reason" [class.on]="reason === r">
                <input type="radio" name="rq-reason" [value]="r" [(ngModel)]="reason" />
                <span>{{ r }}</span>
              </label>
            }
          </div>
          <label class="rq-field">Detalles (opcional)
            <textarea [(ngModel)]="details" name="rq-details" maxlength="1000" rows="3"
              placeholder="Ej. Según el Código Nacional de Tránsito, la respuesta correcta es la B"></textarea>
          </label>
          @if (error()) { <p class="rq-error" role="alert">{{ error() }}</p> }
          <div class="rq-actions">
            <ui-button type="button" variant="ghost" (click)="close()">Cancelar</ui-button>
            <ui-button type="button" [loading]="sending()" [disabled]="!reason" (click)="send()">Enviar reporte</ui-button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    :host { display: block; }
    .rq-link {
      border: 0;
      background: transparent;
      padding: 0.35rem 0;
      color: var(--color-text-secondary);
      font: inherit;
      font-size: var(--text-sm);
      font-weight: 700;
      cursor: pointer;
    }
    .rq-link:hover { color: var(--color-danger, #b91c1c); }
    .rq-done { font-size: var(--text-sm); color: var(--color-success); font-weight: 700; }
    .rq-overlay {
      position: fixed;
      inset: 0;
      z-index: var(--z-modal, 1000);
      background: var(--color-scrim, rgba(0, 0, 0, 0.5));
      display: grid;
      place-items: center;
      padding: 1rem;
    }
    .rq-sheet {
      width: min(520px, 100%);
      max-height: calc(100dvh - 2rem);
      overflow-y: auto;
      display: grid;
      gap: 0.85rem;
      padding: 1.2rem;
      border-radius: 1.1rem;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      box-shadow: var(--shadow-md);
      color: var(--color-text);
      text-align: left;
    }
    .rq-sheet h2 { margin: 0; font-size: var(--text-lg); }
    .rq-q { margin: 0; color: var(--color-text-secondary); font-size: var(--text-sm); overflow-wrap: anywhere; }
    .rq-reasons { display: grid; gap: 0.45rem; }
    .rq-reason {
      display: flex;
      gap: 0.6rem;
      align-items: center;
      padding: 0.6rem 0.75rem;
      border-radius: 0.7rem;
      border: 1px solid var(--color-border);
      cursor: pointer;
      font-size: var(--text-sm);
      font-weight: 600;
    }
    .rq-reason.on { border-color: var(--color-primary); background: var(--color-primary-soft); }
    .rq-reason input { accent-color: var(--color-primary); }
    .rq-field { display: grid; gap: 0.35rem; font-size: var(--text-sm); font-weight: 700; }
    .rq-field textarea {
      font: inherit;
      font-weight: 400;
      padding: 0.6rem 0.75rem;
      border-radius: 0.7rem;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      resize: vertical;
    }
    .rq-error { margin: 0; color: var(--color-danger, #b91c1c); font-size: var(--text-sm); }
    .rq-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 0.5rem; }
    @media (max-width: 600px) {
      .rq-overlay { padding: 0; place-items: end stretch; }
      .rq-sheet { width: 100%; border-radius: 1.1rem 1.1rem 0 0; max-height: 92dvh; }
      .rq-actions ui-button { flex: 1; }
    }
  `]
})
export class ReportQuestionComponent {
  private readonly api = inject(RequestsApi);

  @Input({ required: true }) questionId!: number;
  @Input() questionText = '';

  readonly reasons = REASONS;
  readonly open = signal(false);
  readonly sending = signal(false);
  readonly sent = signal(false);
  readonly error = signal<string | null>(null);

  reason = '';
  details = '';

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open() && !this.sending()) this.close();
  }

  openSheet(): void {
    this.reason = '';
    this.details = '';
    this.error.set(null);
    this.open.set(true);
  }

  close(): void {
    this.open.set(false);
  }

  send(): void {
    if (!this.reason) return;
    const details = this.details.trim();
    this.sending.set(true);
    this.error.set(null);
    this.api
      .create({
        kind: 'report',
        questionId: this.questionId,
        message: details ? `${this.reason}. ${details}` : this.reason
      })
      .subscribe({
        next: () => {
          this.sending.set(false);
          this.open.set(false);
          this.sent.set(true);
        },
        error: (err) => {
          this.sending.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }
}
