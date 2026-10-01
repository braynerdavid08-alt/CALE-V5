import { Component, Input, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { CandlePoint, UiCandleChartComponent } from '../../../shared/ui/charts/ui-candle-chart.component';
import { ChartTone, LineChartPoint, UiLineChartComponent } from '../../../shared/ui/charts/ui-line-chart.component';
import { ProgressApi, ProgressAttemptDto, ProgressMode, StudentProgressDto } from '../api/progress.api';

type Metric = 'score' | 'duration';

const TZ = 'America/Bogota';
const dateFmt = new Intl.DateTimeFormat('es-CO', { timeZone: TZ, day: 'numeric', month: 'long', year: 'numeric' });
const timeFmt = new Intl.DateTimeFormat('es-CO', { timeZone: TZ, hour: 'numeric', minute: '2-digit' });
const shortMonth = new Intl.DateTimeFormat('es-CO', { timeZone: 'UTC', month: 'short' });

/** Real exam progress of a student: KPIs, results/duration lines, weekly candles. Data comes from the API only. */
@Component({
  selector: 'app-progress-dashboard',
  standalone: true,
  imports: [RouterLink, UiLineChartComponent, UiCandleChartComponent],
  templateUrl: './progress-dashboard.component.html',
  styleUrl: './progress-dashboard.component.css'
})
export class ProgressDashboardComponent implements OnInit {
  private readonly api = inject(ProgressApi);

  /** When set, shows that student of the authenticated school instead of the current user. */
  @Input() studentUserId: number | null = null;
  @Input() heading = 'Mi progreso en exámenes';
  @Input() subheading = 'Así han evolucionado tus resultados en cada intento.';
  @Input() emptyLink: string | null = '/student/simulator';

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly data = signal<StudentProgressDto | null>(null);

  readonly mode = signal<ProgressMode>('all');
  readonly take = signal<number | null>(null);
  readonly metric = signal<Metric>('score');

  readonly takeOptions: { value: number | null; label: string }[] = [
    { value: null, label: 'Todos' },
    { value: 5, label: 'Últimos 5' },
    { value: 10, label: 'Últimos 10' }
  ];
  readonly modeOptions: { value: ProgressMode; label: string }[] = [
    { value: 'all', label: 'Todos los modos' },
    { value: 'exam', label: 'Examen' },
    { value: 'practice', label: 'Práctica' }
  ];

  readonly showModes = computed(() => {
    const c = this.data()?.modeCounts;
    return !!c && c.exam > 0 && c.practice > 0;
  });

  readonly hasData = computed(() => (this.data()?.totalAttempts ?? 0) > 0);

  readonly scorePoints = computed<LineChartPoint[]>(() =>
    (this.data()?.attempts ?? []).map((a) => ({
      label: `${a.number}`,
      value: a.score,
      tone: this.bandTone(a.band),
      ref: a.passThreshold,
      title: `Intento #${a.number}`,
      lines: this.tooltipLines(a)
    }))
  );

  readonly durationPoints = computed<LineChartPoint[]>(() =>
    (this.data()?.attempts ?? []).map((a) => ({
      label: `${a.number}`,
      value: a.durationSeconds === null ? null : Math.round((a.durationSeconds / 60) * 10) / 10,
      tone: 'violet' as ChartTone,
      title: `Intento #${a.number}`,
      lines: this.tooltipLines(a)
    }))
  );

  readonly durationMax = computed(() => {
    const mins = (this.data()?.attempts ?? [])
      .map((a) => (a.durationSeconds ?? 0) / 60);
    const top = Math.max(5, ...mins);
    return Math.ceil(top / 5) * 5;
  });

  readonly candles = computed<CandlePoint[]>(() =>
    (this.data()?.weeks ?? []).map((w, i) => {
      const tone: ChartTone = w.count === 1
        ? 'primary'
        : w.close > w.open
          ? 'success'
          : w.close < w.open
            ? 'danger'
            : 'warning';
      const range = this.weekRange(w.weekStart, w.weekEnd);
      return {
        label: `Sem ${i + 1}`,
        sublabel: range,
        open: w.open,
        close: w.close,
        high: w.high,
        low: w.low,
        tone,
        title: `Semana del ${range}`,
        lines: [
          `Intentos: ${w.count}`,
          `Primer resultado: ${this.pct(w.open)}`,
          `Último resultado: ${this.pct(w.close)}`,
          `Mejor: ${this.pct(w.high)} · Peor: ${this.pct(w.low)}`,
          `Promedio: ${this.pct(w.average)}`
        ]
      };
    })
  );

  readonly approvalLabel = computed(() => {
    const d = this.data();
    if (!d) return '';
    return d.approvalThreshold !== null
      ? `Aprueba: ${this.pct(d.approvalThreshold)}`
      : 'Mínimo para aprobar';
  });

  readonly trendText = computed(() => {
    const d = this.data();
    if (!d || d.trendDirection === 'none' || d.trend === null) return null;
    const sign = d.trend > 0 ? '+' : '';
    const word = d.trendDirection === 'up' ? 'Subió' : d.trendDirection === 'down' ? 'Bajó' : 'Se mantiene';
    return { value: `${sign}${this.num(d.trend)} pts`, word, dir: d.trendDirection };
  });

  readonly trendExplain = computed(() => {
    const d = this.data();
    if (!d || d.trendWindow < 1 || d.recentAverage === null || d.previousAverage === null) {
      return 'La tendencia aparece desde el segundo intento.';
    }
    const k = d.trendWindow;
    const label = k === 1 ? 'el último intento' : `los últimos ${k} intentos`;
    const prev = k === 1 ? 'el anterior' : `los ${k} anteriores`;
    return `Compara el promedio de ${label} (${this.pct(d.recentAverage)}) con ${prev} (${this.pct(d.previousAverage)}).`;
  });

  readonly passRuleText = computed(() => {
    const d = this.data();
    if (!d) return '';
    return `Se aprueba con máximo ${d.maxIncorrectAnswers} respuestas incorrectas.`;
  });

  ngOnInit(): void {
    this.load();
  }

  setTake(value: number | null): void {
    if (this.take() === value) return;
    this.take.set(value);
    this.load();
  }

  setMode(value: ProgressMode): void {
    if (this.mode() === value) return;
    this.mode.set(value);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.get({ mode: this.mode(), take: this.take(), studentUserId: this.studentUserId }).subscribe({
      next: (res) => {
        this.data.set(res);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  pct(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return `${this.num(value)}%`;
  }

  num(value: number): string {
    return value.toLocaleString('es-CO', { maximumFractionDigits: 1 });
  }

  duration(seconds: number | null | undefined): string {
    if (seconds === null || seconds === undefined) return '—';
    const s = Math.max(0, Math.round(seconds));
    if (s < 60) return `${s} s`;
    const h = Math.floor(s / 3600);
    const m = Math.floor((s % 3600) / 60);
    const rest = s % 60;
    if (h > 0) return `${h} h ${m} min`;
    return rest ? `${m} min ${rest} s` : `${m} min`;
  }

  minutesTick = (value: number): string => `${value} min`;
  percentTick = (value: number): string => `${value}%`;

  bandLabel(band: string): string {
    if (band === 'passed') return 'Aprobado';
    if (band === 'near') return 'Cerca de aprobar';
    return 'No aprobado';
  }

  modeLabel(mode: string): string {
    if (mode === 'exam') return 'Examen';
    if (mode === 'mixed_practice') return 'Simulacro personalizado';
    if (mode === 'practice') return 'Práctica';
    return mode || 'Evaluación';
  }

  private bandTone(band: string): ChartTone {
    if (band === 'passed') return 'success';
    if (band === 'near') return 'warning';
    return 'danger';
  }

  private tooltipLines(a: ProgressAttemptDto): string[] {
    const when = new Date(a.finishedAt);
    return [
      `Fecha: ${dateFmt.format(when)}`,
      `Hora: ${timeFmt.format(when)}`,
      `Resultado: ${this.pct(a.score)} (${a.correctCount} de ${a.totalQuestions})`,
      `Estado: ${this.bandLabel(a.band)}`,
      `Duración: ${a.durationSeconds === null ? 'Sin registro' : this.duration(a.durationSeconds)}`,
      `Modo: ${this.modeLabel(a.mode)}`
    ];
  }

  private weekRange(start: string, end: string): string {
    const s = new Date(`${start}T00:00:00Z`);
    const e = new Date(`${end}T00:00:00Z`);
    const sm = shortMonth.format(s).replace('.', '');
    const em = shortMonth.format(e).replace('.', '');
    return sm === em
      ? `${s.getUTCDate()}–${e.getUTCDate()} ${em}`
      : `${s.getUTCDate()} ${sm}–${e.getUTCDate()} ${em}`;
  }
}
