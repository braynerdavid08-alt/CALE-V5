import { Component, Input } from '@angular/core';

@Component({
  selector: 'ui-badge',
  standalone: true,
  template: `<span class="badge" [attr.data-tone]="tone"><ng-content /></span>`,
  styles: [`
    .badge {
      display: inline-flex;
      align-items: center;
      min-height: 1.6rem;
      padding: 0.2rem 0.55rem;
      border-radius: var(--radius-sm);
      background: var(--color-chip);
      color: var(--color-text);
      font-size: var(--text-xs);
      font-weight: 700;
      line-height: 1.2;
      white-space: nowrap;
    }
    .badge[data-tone='success'] { background: var(--color-success-soft); color: var(--color-success); }
    .badge[data-tone='danger'] { background: var(--color-danger-soft); color: var(--color-danger); }
    .badge[data-tone='warning'] { background: var(--color-warning-soft); color: var(--color-warning); }
    .badge[data-tone='primary'] { background: var(--color-primary-soft); color: var(--color-primary); }
  `]
})
export class UiBadgeComponent {
  @Input() tone: 'neutral' | 'success' | 'danger' | 'warning' | 'primary' = 'neutral';
}
