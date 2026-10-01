import {
  AfterViewChecked,
  ChangeDetectionStrategy,
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

/** Panel with free content: bottom sheet on phones, centered window on desktop. */
@Component({
  selector: 'ui-sheet',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open) {
      <div class="overlay" (click)="close.emit()">
        <div
          #sheetEl
          class="sheet"
          role="dialog"
          aria-modal="true"
          [attr.aria-labelledby]="titleId"
          tabindex="-1"
          (click)="$event.stopPropagation()">
          <header class="head">
            <h2 [id]="titleId">{{ title }}</h2>
            <button type="button" class="x" aria-label="Cerrar" (click)="close.emit()">✕</button>
          </header>
          <div class="body"><ng-content /></div>
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
    .sheet {
      width: min(36rem, 100%);
      max-height: calc(100dvh - 2rem);
      display: flex;
      flex-direction: column;
      background: var(--color-surface);
      color: var(--color-text);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-md);
      outline: none;
    }
    .head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      padding: 1rem 1.15rem 0.75rem;
      border-bottom: 1px solid var(--color-border);
    }
    h2 { margin: 0; font-size: var(--text-xl); line-height: 1.25; }
    .x {
      flex: none;
      width: 3rem;
      height: 3rem;
      border: 1px solid var(--color-border);
      border-radius: 999px;
      background: transparent;
      color: var(--color-text);
      font-size: 1.15rem;
      cursor: pointer;
    }
    .x:hover, .x:focus-visible { border-color: var(--color-primary); color: var(--color-primary); }
    .body {
      padding: 1rem 1.15rem 1.25rem;
      overflow-y: auto;
      overscroll-behavior: contain;
    }
    @media (max-width: 600px) {
      .overlay { place-items: end stretch; padding: 0; }
      .sheet {
        width: 100%;
        max-height: 92dvh;
        border-radius: var(--radius-lg) var(--radius-lg) 0 0;
        padding-bottom: var(--safe-bottom, 0px);
      }
    }
  `]
})
export class UiSheetComponent implements OnChanges, AfterViewChecked {
  @Input() open = false;
  @Input() title = '';
  @Output() close = new EventEmitter<void>();
  @ViewChild('sheetEl') sheetEl?: ElementRef<HTMLElement>;

  readonly titleId = 'sheet-' + Math.random().toString(36).slice(2, 8);

  private previousFocus: HTMLElement | null = null;
  private needsFocus = false;

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['open']) return;
    if (this.open) {
      this.previousFocus = document.activeElement as HTMLElement | null;
      this.needsFocus = true;
    } else if (changes['open'].previousValue) {
      queueMicrotask(() => this.previousFocus?.focus?.());
    }
  }

  ngAfterViewChecked(): void {
    if (!this.needsFocus || !this.sheetEl) return;
    this.needsFocus = false;
    this.sheetEl.nativeElement.focus();
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (!this.open || event.key !== 'Escape' || event.defaultPrevented) return;
    event.preventDefault();
    this.close.emit();
  }
}
