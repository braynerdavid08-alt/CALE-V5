import { Component, Input } from '@angular/core';

export interface DashDonutSlice {
  label: string;
  value: number;
  color: string;
}

@Component({
  selector: 'ui-dash-donut',
  standalone: true,
  template: `
    <div class="wrap">
      <div
        class="chart"
        role="img"
        [attr.aria-label]="centerLabel || 'Distribución'"
        [style.background]="gradient">
        <div class="hole">
          <span>{{ centerLabel }}</span>
        </div>
      </div>
      <ul class="legend">
        @for (s of withPct; track s.label) {
          <li>
            <span class="dot" [style.background]="s.color"></span>
            <span>{{ s.label }}</span>
            <strong>{{ s.pct }}%</strong>
          </li>
        }
      </ul>
    </div>
  `,
  styles: [`
    .wrap {
      display: grid;
      grid-template-columns: 10rem minmax(0, 1fr);
      gap: 1.1rem;
      align-items: center;
    }
    .chart {
      width: 10rem;
      height: 10rem;
      border-radius: 50%;
      display: grid;
      place-items: center;
    }
    .hole {
      width: 6rem;
      height: 6rem;
      border-radius: 50%;
      background: var(--color-surface);
      display: grid;
      place-items: center;
      text-align: center;
      padding: 0.4rem;
      font-size: var(--text-sm);
      font-weight: 800;
      color: var(--color-text);
      line-height: 1.2;
    }
    .legend {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.55rem;
    }
    .legend li {
      display: grid;
      grid-template-columns: auto 1fr auto;
      gap: 0.55rem;
      align-items: center;
      font-size: var(--text-sm);
    }
    .dot {
      width: 0.6rem;
      height: 0.6rem;
      border-radius: 999px;
    }
    @media (max-width: 520px) {
      .wrap { grid-template-columns: 1fr; justify-items: center; }
      .legend { width: 100%; }
    }
  `]
})
export class UiDashDonutComponent {
  @Input() slices: DashDonutSlice[] = [];
  @Input() centerLabel = '';

  get withPct(): (DashDonutSlice & { pct: string })[] {
    const total = this.slices.reduce((s, x) => s + Math.max(0, x.value), 0);
    return this.slices.map((s) => ({
      ...s,
      pct: total <= 0 ? '0' : ((s.value / total) * 100).toFixed(1)
    }));
  }

  get gradient(): string {
    const total = this.slices.reduce((s, x) => s + Math.max(0, x.value), 0);
    if (total <= 0) {
      return 'var(--color-chip)';
    }
    let cursor = 0;
    const parts: string[] = [];
    for (const s of this.slices) {
      if (s.value <= 0) {
        continue;
      }
      const start = (cursor / total) * 360;
      cursor += s.value;
      const end = (cursor / total) * 360;
      parts.push(`${s.color} ${start}deg ${end}deg`);
    }
    return `conic-gradient(${parts.join(', ')})`;
  }
}
