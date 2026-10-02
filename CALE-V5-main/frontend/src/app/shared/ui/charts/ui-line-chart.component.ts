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

export type ChartTone = 'success' | 'warning' | 'danger' | 'primary' | 'violet';

export interface LineChartPoint {
  /** X axis label (e.g. attempt number). */
  label: string;
  /** Null = no value; the point is skipped and the line is broken there. */
  value: number | null;
  tone: ChartTone;
  /** Tooltip title and lines; also used as the accessible name. */
  title: string;
  lines: string[];
  /** Optional reference value for this x (e.g. pass threshold). */
  ref?: number | null;
}

interface Plotted {
  index: number;
  x: number;
  y: number;
  point: LineChartPoint;
}

const PAD = { top: 16, right: 18, bottom: 34, left: 48 };

/** Responsive SVG line chart with hoverable / focusable points. No external dependency. */
@Component({
  selector: 'ui-line-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap" [style.height.px]="height">
      <svg
        [attr.width]="width()"
        [attr.height]="height"
        [attr.aria-label]="ariaLabel"
        role="img"
        (mouseleave)="active.set(null)">
        <defs>
          <linearGradient [attr.id]="gradId" x1="0" x2="0" y1="0" y2="1">
            <stop offset="0%" [attr.stop-color]="'var(--chart-' + lineTone + ')'" stop-opacity="0.32" />
            <stop offset="100%" [attr.stop-color]="'var(--chart-' + lineTone + ')'" stop-opacity="0" />
          </linearGradient>
        </defs>

        @for (t of yTicks(); track t.value) {
          <line class="grid" [attr.x1]="padLeft" [attr.x2]="width() - padRight" [attr.y1]="t.y" [attr.y2]="t.y" />
          <text class="tick" [attr.x]="padLeft - 8" [attr.y]="t.y + 4" text-anchor="end">{{ t.label }}</text>
        }
        @for (t of xTicks(); track t.index) {
          <text class="tick" [attr.x]="t.x" [attr.y]="height - 12" text-anchor="middle">{{ t.label }}</text>
        }
        @if (yTitle) {
          <text class="axis-title" [attr.transform]="'translate(12 ' + (padTop + plotH() / 2) + ') rotate(-90)'" text-anchor="middle">{{ yTitle }}</text>
        }

        @if (areaPath()) {
          <path class="area" [attr.d]="areaPath()" [attr.fill]="'url(#' + gradId + ')'" />
        }
        @if (refPath(); as rp) {
          <path class="ref" [attr.d]="rp.d" />
          <text class="ref-label" [attr.x]="padLeft + 6" [attr.y]="rp.labelY" text-anchor="start">{{ rp.label }}</text>
        }
        @for (seg of segments(); track $index) {
          <path class="line" [attr.d]="seg" [style.stroke]="'var(--chart-' + lineTone + ')'" />
        }
        @for (p of plotted(); track p.index) {
          <circle
            class="dot"
            [class.on]="active() === p.index"
            [attr.cx]="p.x"
            [attr.cy]="p.y"
            [attr.r]="dotRadius()"
            [style.fill]="'var(--chart-' + p.point.tone + ')'"
            tabindex="0"
            role="button"
            [attr.aria-label]="p.point.title + '. ' + p.point.lines.join('. ')"
            (mouseenter)="active.set(p.index)"
            (focus)="active.set(p.index)"
            (blur)="active.set(null)"
            (click)="toggle(p.index)" />
        }
      </svg>

      @if (activePoint(); as ap) {
        <div class="tip" role="status" [style.left.px]="tipLeft(ap.x)" [style.top.px]="tipTop(ap.y)">
          <strong>{{ ap.point.title }}</strong>
          @for (line of ap.point.lines; track $index) {
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
      --chart-violet: #a78bfa;
    }
    .wrap { position: relative; width: 100%; }
    svg { display: block; overflow: visible; }
    .grid { stroke: var(--color-border); stroke-width: 1; opacity: 0.7; }
    .tick { fill: var(--color-text-secondary); font-size: 0.72rem; font-weight: 600; }
    .axis-title { fill: var(--color-text-secondary); font-size: 0.72rem; font-weight: 700; }
    .line { fill: none; stroke-width: 2.5; stroke-linejoin: round; stroke-linecap: round; }
    .ref { fill: none; stroke: var(--color-text-secondary); stroke-width: 1.5; stroke-dasharray: 6 5; opacity: 0.9; }
    .ref-label { fill: var(--color-text); font-size: 0.72rem; font-weight: 800; paint-order: stroke; stroke: var(--color-surface); stroke-width: 4px; }
    .dot { stroke: var(--color-surface); stroke-width: 2; cursor: pointer; transition: r 0.12s ease; outline: none; }
    .dot.on, .dot:focus-visible { stroke: var(--color-text); stroke-width: 2.5; }
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
export class UiLineChartComponent implements AfterViewInit, OnDestroy {
  private readonly pointsSig = signal<LineChartPoint[]>([]);
  private readonly minSig = signal(0);
  private readonly maxSig = signal(100);

  @Input() set points(value: LineChartPoint[]) { this.pointsSig.set(value ?? []); }
  @Input() set min(value: number) { this.minSig.set(value); }
  @Input() set max(value: number) { this.maxSig.set(value); }
  @Input() height = 260;
  @Input() lineTone: ChartTone = 'primary';
  @Input() yTitle = '';
  @Input() ariaLabel = '';
  @Input() refLabel = '';
  @Input() tickFormat: (value: number) => string = (v) => `${v}`;
  @Input() tickCount = 5;

  readonly padTop = PAD.top;
  readonly padLeft = PAD.left;
  readonly padRight = PAD.right;
  readonly gradId = 'lc-' + Math.random().toString(36).slice(2, 8);
  readonly width = signal(600);
  readonly active = signal<number | null>(null);

  private observer?: ResizeObserver;

  constructor(private readonly host: ElementRef<HTMLElement>) {}

  ngAfterViewInit(): void {
    const el = this.host.nativeElement;
    this.width.set(Math.max(240, el.clientWidth || 600));
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
  private readonly plotW = computed(() => this.width() - PAD.left - PAD.right);

  private xAt(index: number, count: number): number {
    if (count <= 1) return PAD.left + this.plotW() / 2;
    return PAD.left + (this.plotW() * index) / (count - 1);
  }

  private yAt(value: number): number {
    const min = this.minSig();
    const span = Math.max(1e-9, this.maxSig() - min);
    const clamped = Math.min(this.maxSig(), Math.max(min, value));
    return PAD.top + this.plotH() * (1 - (clamped - min) / span);
  }

  readonly plotted = computed<Plotted[]>(() => {
    const pts = this.pointsSig();
    return pts
      .map((point, index) => ({ point, index }))
      .filter((p) => p.point.value !== null && p.point.value !== undefined)
      .map(({ point, index }) => ({
        index,
        point,
        x: this.xAt(index, pts.length),
        y: this.yAt(point.value as number)
      }));
  });

  readonly dotRadius = computed(() => (this.pointsSig().length > 60 ? 3 : this.pointsSig().length > 30 ? 4 : 5.5));

  readonly segments = computed<string[]>(() => {
    const pts = this.pointsSig();
    const result: string[] = [];
    let current: string[] = [];
    pts.forEach((p, i) => {
      if (p.value === null || p.value === undefined) {
        if (current.length > 1) result.push(current.join(' '));
        current = [];
        return;
      }
      const cmd = current.length ? 'L' : 'M';
      current.push(`${cmd}${this.xAt(i, pts.length).toFixed(1)},${this.yAt(p.value).toFixed(1)}`);
    });
    if (current.length > 1) result.push(current.join(' '));
    return result;
  });

  readonly areaPath = computed(() => {
    const pts = this.plotted();
    if (pts.length < 2) return '';
    const base = PAD.top + this.plotH();
    const line = pts.map((p, i) => `${i ? 'L' : 'M'}${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');
    return `${line} L${pts[pts.length - 1].x.toFixed(1)},${base} L${pts[0].x.toFixed(1)},${base} Z`;
  });

  readonly refPath = computed(() => {
    const pts = this.pointsSig();
    const withRef = pts
      .map((p, i) => ({ ref: p.ref, i }))
      .filter((p) => p.ref !== null && p.ref !== undefined) as { ref: number; i: number }[];
    if (!withRef.length) return null;
    const count = pts.length;
    const firstRef = withRef[0].ref;
    const lastRef = withRef[withRef.length - 1].ref;
    const coords: [number, number][] = [[PAD.left, this.yAt(withRef[0].ref)]];
    if (count > 1) {
      for (const p of withRef) coords.push([this.xAt(p.i, count), this.yAt(p.ref)]);
    }
    coords.push([this.width() - PAD.right, this.yAt(lastRef)]);
    const d = coords.map(([x, y], k) => `${k ? 'L' : 'M'}${x.toFixed(1)},${y.toFixed(1)}`).join(' ');
    return { d, label: this.refLabel, labelY: this.yAt(firstRef) - 7 };
  });

  readonly yTicks = computed(() => {
    const min = this.minSig();
    const max = this.maxSig();
    const steps = Math.max(1, this.tickCount);
    return Array.from({ length: steps + 1 }, (_, i) => {
      const value = min + ((max - min) * i) / steps;
      return { value, y: this.yAt(value), label: this.tickFormat(Math.round(value * 10) / 10) };
    });
  });

  readonly xTicks = computed(() => {
    const pts = this.pointsSig();
    const maxLabels = Math.max(2, Math.floor(this.plotW() / 34));
    const every = Math.max(1, Math.ceil(pts.length / maxLabels));
    return pts
      .map((p, index) => ({ index, label: p.label, x: this.xAt(index, pts.length) }))
      .filter((t) => t.index % every === 0 || t.index === pts.length - 1);
  });

  readonly activePoint = computed(() => {
    const idx = this.active();
    return idx === null ? null : this.plotted().find((p) => p.index === idx) ?? null;
  });

  toggle(index: number): void {
    this.active.set(this.active() === index ? null : index);
  }

  tipLeft(x: number): number {
    const tipW = 240;
    return Math.min(Math.max(4, x - tipW / 2), Math.max(4, this.width() - tipW - 4));
  }

  tipTop(y: number): number {
    return y > this.height / 2 ? Math.max(0, y - 150) : y + 14;
  }
}
