import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Input,
  OnDestroy,
  computed,
  signal
} from '@angular/core';
import { ChartTone } from './ui-line-chart.component';

export interface BarPoint {
  label: string;
  sublabel: string;
  value: number;
  tone: ChartTone;
  title: string;
  lines: string[];
}

const PAD = { top: 24, right: 12, bottom: 44, left: 44 };

/** Responsive SVG bar chart with the value printed above each bar. */
@Component({
  selector: 'ui-bar-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap" [style.height.px]="height">
      <svg [attr.width]="width()" [attr.height]="height" role="img" [attr.aria-label]="ariaLabel" (mouseleave)="active.set(null)">
        @for (t of yTicks(); track t.value) {
          <line class="grid" [attr.x1]="padLeft" [attr.x2]="width() - padRight" [attr.y1]="t.y" [attr.y2]="t.y" />
          <text class="tick" [attr.x]="padLeft - 8" [attr.y]="t.y + 4" text-anchor="end">{{ t.label }}</text>
        }
        @for (b of bars(); track b.index) {
          <g
            class="bar"
            [class.on]="active() === b.index"
            tabindex="0"
            role="button"
            [attr.aria-label]="b.point.title + '. ' + b.point.lines.join('. ')"
            [style.--tone]="'var(--chart-' + b.point.tone + ')'"
            (mouseenter)="active.set(b.index)"
            (focus)="active.set(b.index)"
            (blur)="active.set(null)"
            (click)="active.set(active() === b.index ? null : b.index)">
            <rect class="hit" [attr.x]="b.x - b.slot / 2" [attr.y]="padTop" [attr.width]="b.slot" [attr.height]="plotH()" />
            <rect class="body" [attr.x]="b.x - b.barW / 2" [attr.y]="b.y" [attr.width]="b.barW" [attr.height]="b.h" rx="6" />
            <text class="value" [attr.x]="b.x" [attr.y]="b.y - 6" text-anchor="middle">{{ valueFormat(b.point.value) }}</text>
            <text class="tick label" [attr.x]="b.x" [attr.y]="height - 26" text-anchor="middle">{{ b.point.label }}</text>
            @if (b.slot >= 96) {
              <text class="tick sub" [attr.x]="b.x" [attr.y]="height - 10" text-anchor="middle">{{ b.point.sublabel }}</text>
            }
          </g>
        }
      </svg>
      @if (activeBar(); as ab) {
        <div class="tip" role="status" [style.left.px]="tipLeft(ab.x)" [style.top.px]="8">
          <strong>{{ ab.point.title }}</strong>
          @for (line of ab.point.lines; track $index) {
            <span>{{ line }}</span>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host {
      display: block;
      --chart-success: var(--color-success, #22c55e);
      --chart-warning: var(--color-warning, #eab308);
      --chart-danger: var(--color-danger, #ef4444);
      --chart-primary: var(--color-primary, #4ade80);
    }
    .wrap { position: relative; width: 100%; }
    svg { display: block; overflow: visible; }
    .grid { stroke: var(--color-border); opacity: 0.7; }
    .tick { fill: var(--color-text-secondary); font-size: 0.72rem; font-weight: 600; }
    .tick.label { fill: var(--color-text); font-weight: 800; }
    .value { fill: var(--color-text); font-size: 0.85rem; font-weight: 800; }
    .bar { cursor: pointer; outline: none; }
    .hit { fill: transparent; }
    .bar.on .hit, .bar:focus-visible .hit { fill: color-mix(in srgb, var(--color-text) 6%, transparent); }
    .body { fill: var(--tone); }
    .tip {
      position: absolute;
      z-index: 2;
      display: grid;
      gap: 0.15rem;
      min-width: 11rem;
      max-width: 15rem;
      padding: 0.6rem 0.75rem;
      border: 1px solid var(--color-border);
      border-radius: 0.75rem;
      background: var(--color-surface);
      box-shadow: 0 12px 30px rgba(2, 8, 15, 0.35);
      font-size: 0.82rem;
      line-height: 1.35;
      pointer-events: none;
    }
    .tip strong { font-size: 0.9rem; margin-bottom: 0.15rem; }
    .tip span { color: var(--color-text-secondary); }
  `]
})
export class UiBarChartComponent implements AfterViewInit, OnDestroy {
  private readonly pointsSig = signal<BarPoint[]>([]);

  @Input() set points(value: BarPoint[]) { this.pointsSig.set(value ?? []); }
  @Input() height = 240;
  @Input() min = 0;
  @Input() max = 100;
  @Input() ariaLabel = '';
  @Input() tickFormat: (value: number) => string = (v) => `${v}`;
  @Input() valueFormat: (value: number) => string = (v) => `${v}`;

  readonly padTop = PAD.top;
  readonly padLeft = PAD.left;
  readonly padRight = PAD.right;
  readonly width = signal(480);
  readonly active = signal<number | null>(null);
  private observer?: ResizeObserver;

  constructor(private readonly host: ElementRef<HTMLElement>) {}

  ngAfterViewInit(): void {
    const el = this.host.nativeElement;
    this.width.set(Math.max(240, el.clientWidth || 480));
    if (typeof ResizeObserver !== 'undefined') {
      this.observer = new ResizeObserver((entries) => {
        const w = Math.floor(entries[0]?.contentRect.width ?? 0);
        if (w > 0 && w !== this.width()) this.width.set(Math.max(240, w));
      });
      this.observer.observe(el);
    }
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  readonly plotH = computed(() => this.height - PAD.top - PAD.bottom);

  private yAt(value: number): number {
    const span = Math.max(1e-9, this.max - this.min);
    const clamped = Math.min(this.max, Math.max(this.min, value));
    return PAD.top + this.plotH() * (1 - (clamped - this.min) / span);
  }

  readonly yTicks = computed(() =>
    Array.from({ length: 6 }, (_, i) => {
      const value = this.min + ((this.max - this.min) * i) / 5;
      return { value, y: this.yAt(value), label: this.tickFormat(value) };
    })
  );

  readonly bars = computed(() => {
    const pts = this.pointsSig();
    const plotW = this.width() - PAD.left - PAD.right;
    const slot = plotW / Math.max(1, pts.length);
    const barW = Math.max(14, Math.min(56, slot * 0.55));
    const base = this.yAt(this.min);
    return pts.map((point, index) => {
      const y = this.yAt(point.value);
      return {
        index,
        point,
        x: PAD.left + slot * (index + 0.5),
        slot,
        barW,
        y: Math.min(y, base - 3),
        h: Math.max(3, base - y)
      };
    });
  });

  readonly activeBar = computed(() => {
    const idx = this.active();
    return idx === null ? null : this.bars()[idx] ?? null;
  });

  tipLeft(x: number): number {
    const tipW = 240;
    return Math.min(Math.max(4, x - tipW / 2), Math.max(4, this.width() - tipW - 4));
  }
}
