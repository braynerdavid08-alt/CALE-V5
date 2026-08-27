import { Component, Input } from '@angular/core';

export interface DashLineSeries {
  label: string;
  color: string;
  values: number[];
}

@Component({
  selector: 'ui-dash-line',
  standalone: true,
  template: `
    <div class="wrap">
      <svg
        class="chart"
        [attr.viewBox]="'0 0 ' + width + ' ' + height"
        role="img"
        [attr.aria-label]="title || 'Gráfico de líneas'">
        @for (y of gridYs; track y) {
          <line
            [attr.x1]="padL"
            [attr.x2]="width - padR"
            [attr.y1]="y"
            [attr.y2]="y"
            class="grid" />
        }
        @for (s of plotted; track s.label) {
          <polyline
            fill="none"
            [attr.stroke]="s.color"
            stroke-width="2.5"
            stroke-linecap="round"
            stroke-linejoin="round"
            [attr.points]="s.points" />
          @for (p of s.dots; track $index) {
            <circle [attr.cx]="p.x" [attr.cy]="p.y" r="3.5" [attr.fill]="s.color" />
          }
        }
        @for (label of labels; track label; let i = $index) {
          <text
            [attr.x]="xAt(i)"
            [attr.y]="height - 8"
            text-anchor="middle"
            class="axis">
            {{ label }}
          </text>
        }
      </svg>
      <ul class="legend">
        @for (s of series; track s.label) {
          <li>
            <span class="swatch" [style.background]="s.color"></span>
            {{ s.label }}
          </li>
        }
      </ul>
    </div>
  `,
  styles: [`
    .wrap { display: grid; gap: 0.75rem; }
    .chart { width: 100%; height: 220px; }
    .grid { stroke: var(--color-border); stroke-width: 1; }
    .axis {
      fill: var(--color-text-secondary);
      font-size: 10px;
      font-family: var(--font);
    }
    .legend {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      font-size: var(--text-sm);
      color: var(--color-text-secondary);
    }
    .legend li { display: inline-flex; align-items: center; gap: 0.4rem; }
    .swatch {
      width: 0.65rem;
      height: 0.65rem;
      border-radius: 999px;
    }
  `]
})
export class UiDashLineComponent {
  @Input() title = '';
  @Input() labels: string[] = [];
  @Input() series: DashLineSeries[] = [];

  readonly width = 480;
  readonly height = 220;
  readonly padL = 16;
  readonly padR = 16;
  readonly padT = 16;
  readonly padB = 28;

  get maxValue(): number {
    const vals = this.series.flatMap((s) => s.values);
    const max = Math.max(0, ...vals);
    return max <= 0 ? 1 : max;
  }

  get gridYs(): number[] {
    const chartH = this.height - this.padT - this.padB;
    return [0, 0.33, 0.66, 1].map((t) => this.padT + chartH * t);
  }

  xAt(i: number): number {
    const n = Math.max(this.labels.length - 1, 1);
    const chartW = this.width - this.padL - this.padR;
    return this.padL + (chartW * i) / n;
  }

  get plotted(): {
    label: string;
    color: string;
    points: string;
    dots: { x: number; y: number }[];
  }[] {
    const chartH = this.height - this.padT - this.padB;
    const max = this.maxValue;
    return this.series.map((s) => {
      const dots = s.values.map((v, i) => {
        const x = this.xAt(i);
        const y = this.padT + chartH * (1 - Math.max(0, v) / max);
        return { x, y };
      });
      return {
        label: s.label,
        color: s.color,
        points: dots.map((p) => `${p.x},${p.y}`).join(' '),
        dots
      };
    });
  }
}
