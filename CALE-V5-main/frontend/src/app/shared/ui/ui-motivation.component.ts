import { Component, Input, OnChanges, OnInit, SimpleChanges, computed, inject } from '@angular/core';
import { MotivationService } from '../../core/motivation/motivation.service';
import { MOTIVATION_CATEGORY_LABEL } from '../../core/motivation/motivation.model';
import { UiIconComponent } from './ui-icon.component';

@Component({
  selector: 'ui-motivation',
  standalone: true,
  imports: [UiIconComponent],
  template: `
    <aside class="motivation" aria-live="polite">
      <span class="icon" aria-hidden="true"><ui-icon name="star" /></span>
      <div class="copy">
        <p class="tag">{{ categoryLabel() }}</p>
        <p class="headline">{{ tip().headline }}</p>
      </div>
    </aside>
  `,
  styles: [`
    :host { display: block; min-width: 0; }

    .motivation {
      display: flex;
      align-items: center;
      gap: 0.55rem;
      min-width: 0;
      max-width: 100%;
      padding: 0.4rem 0.7rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      background: var(--color-primary-soft);
      color: var(--color-text);
    }

    .icon {
      display: grid;
      place-items: center;
      width: 1.7rem;
      height: 1.7rem;
      flex-shrink: 0;
      border-radius: var(--radius-sm);
      background: var(--color-surface);
      color: var(--color-primary);
      border: 1px solid var(--color-border);
    }

    .copy { min-width: 0; }

    .tag {
      margin: 0;
      color: var(--color-primary);
      font-size: 0.65rem;
      font-weight: 700;
      letter-spacing: 0.05em;
      text-transform: uppercase;
      line-height: 1.1;
    }

    .headline {
      margin: 0.1rem 0 0;
      font-size: var(--text-sm);
      font-weight: 650;
      line-height: var(--leading-tight);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    @media (max-width: 700px) {
      .headline {
        white-space: normal;
        display: -webkit-box;
        -webkit-line-clamp: 2;
        -webkit-box-orient: vertical;
      }
    }
  `]
})
export class UiMotivationComponent implements OnInit, OnChanges {
  private readonly motivation = inject(MotivationService);

  @Input() role: string | null | undefined;

  readonly tip = this.motivation.current;
  readonly categoryLabel = computed(
    () => MOTIVATION_CATEGORY_LABEL[this.tip().category]
  );

  ngOnInit(): void {
    this.motivation.ensureSessionTip(this.role);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['role']) {
      this.motivation.ensureSessionTip(this.role);
    }
  }
}
