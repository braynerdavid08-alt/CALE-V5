import {
  AfterViewChecked,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  ViewChild
} from '@angular/core';
import { UiButtonComponent } from './ui-button.component';

@Component({
  selector: 'ui-dialog',
  standalone: true,
  imports: [UiButtonComponent],
  template: `
    @if (open) {
      <div class="overlay" (click)="onCancel()">
        <div
          #dialogEl
          class="dialog"
          role="dialog"
          aria-modal="true"
          [attr.aria-labelledby]="titleId"
          tabindex="-1"
          (click)="$event.stopPropagation()"
          (keydown)="onDialogKeydown($event)">
          <h2 [id]="titleId">{{ title }}</h2>
          <p>{{ message }}</p>
          <div class="actions">
            <ui-button type="button" variant="ghost" (click)="onCancel()">
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
      background: var(--color-scrim);
      display: grid;
      place-items: center;
      padding: var(--spacing-md);
    }
    .dialog {
      width: min(420px, 100%);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: var(--spacing-lg);
      box-shadow: var(--shadow-md);
      color: var(--color-text);
      outline: none;
    }
    h2 { margin: 0 0 0.4rem; font-size: var(--text-lg); }
    p {
      margin: 0 0 var(--spacing-md);
      color: var(--color-text-secondary);
      line-height: var(--leading-body);
    }
    .actions { display: flex; justify-content: flex-end; gap: var(--spacing-sm); }
  `]
})
export class UiDialogComponent implements OnChanges, AfterViewChecked {
  @Input() open = false;
  @Input() title = 'Confirmar';
  @Input() message = '';
  @Input() confirmLabel = 'Confirmar';
  @Input() cancelLabel = 'Cancelar';
  @Input() danger = false;
  @Output() confirm = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  @ViewChild('dialogEl') dialogEl?: ElementRef<HTMLElement>;

  readonly titleId = 'dlg-' + Math.random().toString(36).slice(2, 8);

  private previousFocus: HTMLElement | null = null;
  private needsInitialFocus = false;

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['open']) {
      return;
    }
    if (this.open) {
      this.previousFocus = document.activeElement as HTMLElement | null;
      this.needsInitialFocus = true;
    } else if (changes['open'].previousValue) {
      this.restoreFocus();
    }
  }

  ngAfterViewChecked(): void {
    if (!this.needsInitialFocus || !this.dialogEl) {
      return;
    }
    this.needsInitialFocus = false;
    const root = this.dialogEl.nativeElement;
    const first = this.focusable(root)[0] ?? root;
    first.focus();
  }

  @HostListener('document:keydown', ['$event'])
  onDocumentKeydown(event: KeyboardEvent): void {
    if (!this.open || event.key !== 'Escape') {
      return;
    }
    event.preventDefault();
    this.onCancel();
  }

  onDialogKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Tab' || !this.dialogEl) {
      return;
    }
    const nodes = this.focusable(this.dialogEl.nativeElement);
    if (nodes.length === 0) {
      event.preventDefault();
      this.dialogEl.nativeElement.focus();
      return;
    }
    const first = nodes[0];
    const last = nodes[nodes.length - 1];
    const active = document.activeElement as HTMLElement | null;
    if (event.shiftKey && active === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  onCancel(): void {
    this.cancel.emit();
  }

  private focusable(root: HTMLElement): HTMLElement[] {
    const selector =
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    return Array.from(root.querySelectorAll<HTMLElement>(selector)).filter(
      (el) => !el.hasAttribute('disabled') && el.offsetParent !== null
    );
  }

  private restoreFocus(): void {
    queueMicrotask(() => this.previousFocus?.focus?.());
  }
}
