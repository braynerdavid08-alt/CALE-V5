import { Component, Input } from '@angular/core';

export interface DashBarItem {
  label: string;
  value: number;
  max: number;
  tone?: 'primary' | 'success' | 'warning' | 'info';
}

@Component({
  selector: 'ui-dash-bars',
  standalone: true,
  template: `
    <ul class="bars" role="list">
      @for (item of items; track item.label) {
        <li>
          <div class="row">
            <span>{{ item.label }}</span>
            <strong>{{ item.value }}{{ suffix }}</strong>
          </div>
          <div
            class="track"
            role="progressbar"
            [attr.aria-valuenow]="item.value"
            [attr.aria-valuemin]="0"
            [attr.aria-valuemax]="item.max"
            [attr.aria-label]="item.label">
            <span
              class="fill"
              [attr.data-tone]="item.tone || 'primary'"
              [style.width.%]="pct(item)"></span>
          </div>
        </li>
      }
    </ul>
  `,
  styles: [`
    .bars {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.85rem;
    }
    .row {
      display: flex;
      justify-content: space-between;
      gap: 0.75rem;
      font-size: var(--text-sm);
      margin-bottom: 0.3rem;
    }
    .row strong { font-weight: 750; }
    .track {
      height: 0.55rem;
      border-radius: 999px;
      background: var(--color-chip);
      overflow: hidden;
    }
    .fill {
      display: block;
      height: 100%;
      border-radius: inherit;
      background: var(--color-primary);
      transition: width var(--transition);
    }
    .fill[data-tone='success'] { background: var(--color-success); }
    .fill[data-tone='warning'] { background: var(--color-warning); }
    .fill[data-tone='info'] { background: var(--color-info); }
  `]
})
export class UiDashBarsComponent {
  @Input({ required: true }) items: DashBarItem[] = [];
  @Input() suffix = '';

  pct(item: DashBarItem): number {
    if (!item.max || item.max <= 0) {
      return 0;
    }
    return Math.max(0, Math.min(100, (item.value / item.max) * 100));
  }
}
