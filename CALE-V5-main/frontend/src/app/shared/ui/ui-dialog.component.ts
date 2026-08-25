import { Component, EventEmitter, Input, Output } from '@angular/core';
import { UiButtonComponent } from './ui-button.component';

@Component({
  selector: 'ui-dialog',
  standalone: true,
  imports: [UiButtonComponent],
  template: `
    @if (open) {
      <div class="overlay" (click)="cancel.emit()">
        <div
          class="dialog"
          role="dialog"
          aria-modal="true"
          [attr.aria-labelledby]="titleId"
          (click)="$event.stopPropagation()">
          <h2 [id]="titleId">{{ title }}</h2>
          <p>{{ message }}</p>
          <div class="actions">
            <ui-button type="button" variant="ghost" (click)="cancel.emit()">
              {{ cancelLabel }}
            </ui-button>
            <ui-button type="button" [variant]="danger ? 'danger' : 'primary'" (click)="confirm.emit()">
              {{ confirmLabel }}
            </ui-button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .overlay {
      position: fixed;
      inset: 0;
      z-index: var(--z-modal);
      background: rgba(23, 28, 40, 0.45);
      display: grid;
      place-items: center;
      padding: var(--spacing-md);
    }
    .dialog {
      width: min(420px, 100%);
      background: var(--color-surface);
      border-radius: var(--radius-lg);
      padding: var(--spacing-lg);
      box-shadow: var(--shadow-md);
    }
    h2 { margin: 0 0 0.4rem; font-size: var(--text-lg); }
    p { margin: 0 0 var(--spacing-md); color: var(--color-text-secondary); }
    .actions { display: flex; justify-content: flex-end; gap: var(--spacing-sm); }
  `]
})
export class UiDialogComponent {
  @Input() open = false;
  @Input() title = 'Confirmar';
  @Input() message = '';
  @Input() confirmLabel = 'Confirmar';
  @Input() cancelLabel = 'Cancelar';
  @Input() danger = false;
  @Output() confirm = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  readonly titleId = 'dlg-' + Math.random().toString(36).slice(2, 8);
}
