import { Component, Input, OnChanges, OnInit, SimpleChanges, computed, inject, signal } from '@angular/core';
import { MotivationService } from '../../core/motivation/motivation.service';
import { MOTIVATION_CATEGORY_LABEL } from '../../core/motivation/motivation.model';
import { UiIconComponent } from './ui-icon.component';

@Component({
  selector: 'ui-motivation',
  standalone: true,
  imports: [UiIconComponent],
  template: `
    <aside
      class="motivation"
      [class.compact]="variant === 'compact'"
      [class.card]="variant === 'card'"
      [class.flash]="flash()"
      aria-live="polite"
      (mouseenter)="onPause(true)"
      (mouseleave)="onPause(false)">
      <div class="lead">
        <span class="icon" aria-hidden="true"><ui-icon name="star" /></span>
        <div class="copy">
          <p class="tag">{{ categoryLabel() }}</p>
          <p class="headline">{{ tip().headline }}</p>
          @if (variant !== 'compact') {
            <p class="detail">{{ tip().detail }}</p>
          }
        </div>
      </div>
      <button
        type="button"
        class="next"
        aria-label="Mostrar siguiente consejo de seguridad vial"
        (click)="showNext()">
        Siguiente
      </button>
    </aside>
  `,
  styles: [`
    :host { display: block; min-width: 0; }

    .motivation {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      min-width: 0;
      padding: 0.55rem 0.75rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      background: var(--color-primary-soft);
      color: var(--color-text);
      transition: opacity 180ms ease, transform 180ms ease;
    }

    .motivation.flash {
      opacity: 0.5;
      transform: translateY(2px);
    }

    .motivation.card {
      align-items: flex-start;
      padding: var(--spacing-md) var(--spacing-lg);
      background:
        linear-gradient(135deg, var(--color-welcome-start) 0%, var(--color-welcome-end) 70%);
      box-shadow: var(--shadow-sm);
    }

    .lead {
      display: flex;
      align-items: flex-start;
      gap: 0.65rem;
      min-width: 0;
      flex: 1;
    }

    .icon {
      display: grid;
      place-items: center;
      width: 2rem;
      height: 2rem;
      flex-shrink: 0;
      border-radius: var(--radius-sm);
      background: var(--color-surface);
      color: var(--color-primary);
      border: 1px solid var(--color-border);
    }

    .copy { min-width: 0; }

    .tag {
      margin: 0 0 0.15rem;
      color: var(--color-primary);
      font-size: var(--text-xs);
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
    }

    .headline {
      margin: 0;
      font-size: var(--text-sm);
      font-weight: 700;
      line-height: var(--leading-tight);
    }

    .detail {
      margin: 0.35rem 0 0;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      font-weight: 500;
      line-height: var(--leading-body);
    }

    .compact .headline {
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .next {
      flex-shrink: 0;
      min-height: 2.2rem;
      padding: 0 0.7rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-sm);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
      font-size: var(--text-xs);
      font-weight: 700;
      cursor: pointer;
    }

    .next:hover {
      background: var(--color-chip);
      border-color: var(--color-border-strong);
    }

    @media (max-width: 700px) {
      .compact .headline {
        white-space: normal;
        display: -webkit-box;
        -webkit-line-clamp: 2;
        -webkit-box-orient: vertical;
      }

      .motivation.compact { align-items: flex-start; }
      .compact .next { align-self: center; }
    }
  `]
})
export class UiMotivationComponent implements OnInit, OnChanges {
  private readonly motivation = inject(MotivationService);

  @Input() variant: 'compact' | 'card' = 'compact';
  @Input() role: string | null | undefined;

  readonly tip = this.motivation.current;
  readonly flash = signal(false);
  readonly categoryLabel = computed(
    () => MOTIVATION_CATEGORY_LABEL[this.tip().category]
  );

  ngOnInit(): void {
    this.motivation.setRole(this.role);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['role']) {
      this.motivation.setRole(this.role);
    }
  }

  onPause(paused: boolean): void {
    this.motivation.setPaused(paused);
  }

  showNext(): void {
    this.flash.set(true);
    window.setTimeout(() => {
      this.motivation.next();
      this.flash.set(false);
    }, 140);
  }
}
