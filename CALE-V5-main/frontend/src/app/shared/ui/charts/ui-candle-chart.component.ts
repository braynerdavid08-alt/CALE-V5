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

export interface CandlePoint {
  label: string;
  sublabel: string;
  open: number;
  close: number;
  high: number;
  low: number;
  tone: ChartTone;
  title: string;
  lines: string[];
}

const PAD = { top: 14, right: 12, bottom: 44, left: 44 };

/** Responsive SVG candlestick chart (body = open→close, wick = low→high). */
@Component({
  selector: 'ui-candle-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap" [style.height.px]="height">
      <svg [attr.width]="width()" [attr.height]="height" role="img" [attr.aria-label]="ariaLabel" (mouseleave)="active.set(null)">
        @for (t of yTicks(); track t.value) {
          <line class="grid" [attr.x1]="padLeft" [attr.x2]="width() - padRight" [attr.y1]="t.y" [attr.y2]="t.y" />
          <text class="tick" [attr.x]="padLeft - 8" [attr.y]="t.y + 4" text-anchor="end">{{ t.label }}</text>
        }
        @for (c of candles(); track c.index) {
          <g
            class="candle"
            [class.on]="active() === c.index"
            tabindex="0"
            role="button"
            [attr.aria-label]="c.point.title + '. ' + c.point.lines.join('. ')"
            [style.--tone]="'var(--chart-' + c.point.tone + ')'"
            (mouseenter)="active.set(c.index)"
            (focus)="active.set(c.index)"
            (blur)="active.set(null)"
            (click)="active.set(active() === c.index ? null : c.index)">
            <rect class="hit" [attr.x]="c.x - c.slot / 2" [attr.y]="padTop" [attr.width]="c.slot" [attr.height]="plotH()" />
            <line class="wick" [attr.x1]="c.x" [attr.x2]="c.x" [attr.y1]="c.yHigh" [attr.y2]="c.yLow" />
            <rect class="body" [attr.x]="c.x - c.bodyW / 2" [attr.y]="c.yTop" [attr.width]="c.bodyW" [attr.height]="c.bodyH" rx="3" />
            <text class="tick label" [attr.x]="c.x" [attr.y]="height - 26" text-anchor="middle">{{ c.point.label }}</text>
            <text class="tick sub" [attr.x]="c.x" [attr.y]="height - 10" text-anchor="middle">{{ c.point.sublabel }}</text>
          </g>
        }
      </svg>
      @if (activeCandle(); as ac) {
        <div class="tip" role="status" [style.left.px]="tipLeft(ac.x)" [style.top.px]="8">
          <strong>{{ ac.point.title }}</strong>
          @for (line of ac.point.lines; track $index) {
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
      --chart-primary: var(--color-primary, #4eb6d4);
    }
    .wrap { position: relative; width: 100%; }
    svg { display: block; overflow: visible; }
    .grid { stroke: var(--color-border); opacity: 0.7; }
    .tick { fill: var(--color-text-secondary); font-size: 0.72rem; font-weight: 600; }
    .tick.label { fill: var(--color-text); font-weight: 800; }
    .candle { cursor: pointer; outline: none; }
    .hit { fill: transparent; }
    .candle.on .hit, .candle:focus-visible .hit { fill: color-mix(in srgb, var(--color-text) 6%, transparent); }
    .wick { stroke: var(--tone); stroke-width: 2.5; stroke-linecap: round; }
    .body { fill: var(--tone); stroke: color-mix(in srgb, var(--tone) 70%, #000); stroke-width: 1; }
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
export class UiCandleChartComponent implements AfterViewInit, OnDestroy {
  private readonly pointsSig = signal<CandlePoint[]>([]);

  @Input() set points(value: CandlePoint[]) { this.pointsSig.set(value ?? []); }
  @Input() height = 240;
  @Input() min = 0;
  @Input() max = 100;
  @Input() ariaLabel = '';
  @Input() tickFormat: (value: number) => string = (v) => `${v}`;

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

  readonly candles = computed(() => {
    const pts = this.pointsSig();
    const plotW = this.width() - PAD.left - PAD.right;
    const slot = plotW / Math.max(1, pts.length);
    const bodyW = Math.max(10, Math.min(34, slot * 0.42));
    return pts.map((point, index) => {
      const x = PAD.left + slot * (index + 0.5);
      const yOpen = this.yAt(point.open);
      const yClose = this.yAt(point.close);
      const yTop = Math.min(yOpen, yClose);
      return {
        index,
        point,
        x,
        slot,
        bodyW,
        yTop,
        bodyH: Math.max(3, Math.abs(yOpen - yClose)),
        yHigh: this.yAt(point.high),
        yLow: this.yAt(point.low)
      };
    });
  });

  readonly activeCandle = computed(() => {
    const idx = this.active();
    return idx === null ? null : this.candles()[idx] ?? null;
  });

  tipLeft(x: number): number {
    const tipW = 240;
    return Math.min(Math.max(4, x - tipW / 2), Math.max(4, this.width() - tipW - 4));
  }
}
