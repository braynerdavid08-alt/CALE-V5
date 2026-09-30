import { Component, Input, computed, signal } from '@angular/core';
import { UiBadgeComponent } from './ui-badge.component';
import { UiButtonComponent } from './ui-button.component';

export interface ResultAttempt {
  attemptId: number;
  userId: number;
  userName: string;
  mode: string;
  percent: number;
  passed: boolean;
  startedAt: string;
  finishedAt?: string | null;
}

interface StudentGroup {
  userId: number;
  name: string;
  attempts: ResultAttempt[];
  average: number;
  best: number;
  passedCount: number;
  lastAt: number;
}

type SortKey = 'recent' | 'name' | 'average' | 'attempts';
type StatusFilter = 'all' | 'passed' | 'failed';

const dateFmt = new Intl.DateTimeFormat('es-CO', { day: '2-digit', month: 'short', year: 'numeric' });
const timeFmt = new Intl.DateTimeFormat('es-CO', { hour: 'numeric', minute: '2-digit' });

@Component({
  selector: 'ui-results-by-student',
  standalone: true,
  imports: [UiBadgeComponent, UiButtonComponent],
  template: `
    <section class="summary" aria-label="Resumen">
      <div class="sum"><strong>{{ stats().students }}</strong><span>Estudiantes</span></div>
      <div class="sum"><strong>{{ stats().attempts }}</strong><span>Intentos</span></div>
      <div class="sum"><strong>{{ stats().average }}%</strong><span>Promedio general</span></div>
      <div class="sum"><strong>{{ stats().passRate }}%</strong><span>Tasa de aprobación</span></div>
    </section>

    <div class="toolbar">
      <input
        class="search"
        type="search"
        placeholder="Buscar estudiante…"
        aria-label="Buscar estudiante"
        [value]="query()"
        (input)="query.set($any($event.target).value)" />
      <select aria-label="Modo" [value]="mode()" (change)="mode.set($any($event.target).value)">
        <option value="all">Todos los modos</option>
        <option value="exam">Examen</option>
        <option value="mixed_practice">Simulacro personalizado</option>
        <option value="practice">Práctica</option>
      </select>
      <select aria-label="Estado" [value]="status()" (change)="status.set($any($event.target).value)">
        <option value="all">Todos los estados</option>
        <option value="passed">Aprobados</option>
        <option value="failed">No aprobados</option>
      </select>
      <select aria-label="Ordenar por" [value]="sort()" (change)="sort.set($any($event.target).value)">
        <option value="recent">Actividad más reciente</option>
        <option value="name">Nombre (A-Z)</option>
        <option value="average">Mejor promedio</option>
        <option value="attempts">Más intentos</option>
      </select>
      <span class="grow"></span>
      <ui-button type="button" variant="ghost" (click)="toggleAll()">
        {{ allOpen() ? 'Contraer todos' : 'Expandir todos' }}
      </ui-button>
      <ui-button type="button" variant="secondary" (click)="exportCsv()">Exportar CSV</ui-button>
    </div>

    @if (!groups().length) {
      <p class="none">Ningún resultado coincide con los filtros.</p>
    }

    <div class="list">
      @for (g of groups(); track g.userId) {
        <article class="student" [class.open]="isOpen(g.userId)">
          <button type="button" class="student-head" [attr.aria-expanded]="isOpen(g.userId)" (click)="toggle(g.userId)">
            <span class="avatar" aria-hidden="true">{{ initials(g.name) }}</span>
            <span class="who">
              <strong>{{ g.name || 'Sin nombre' }}</strong>
              <small>Último intento: {{ formatDate(g.lastAt) }}</small>
            </span>
            <span class="metric">
              <strong>{{ g.attempts.length }}</strong>
              <small>{{ g.attempts.length === 1 ? 'intento' : 'intentos' }}</small>
            </span>
            <span class="metric">
              <strong [attr.data-tone]="tone(g.average)">{{ g.average }}%</strong>
              <small>promedio</small>
            </span>
            <span class="metric">
              <strong [attr.data-tone]="tone(g.best)">{{ g.best }}%</strong>
              <small>mejor</small>
            </span>
            <span class="metric">
              <strong>{{ g.passedCount }}/{{ g.attempts.length }}</strong>
              <small>aprobados</small>
            </span>
            <span class="chev" aria-hidden="true">›</span>
          </button>

          @if (isOpen(g.userId)) {
            <div class="table-wrap">
              <table class="data">
                <thead>
                  <tr>
                    <th>Fecha</th>
                    <th>Hora</th>
                    <th>Modo</th>
                    <th>Resultado</th>
                    <th>Duración</th>
                    <th>Estado</th>
                  </tr>
                </thead>
                <tbody>
                  @for (a of g.attempts; track a.attemptId) {
                    <tr>
                      <td data-label="Fecha">{{ formatDate(when(a)) }}</td>
                      <td data-label="Hora">{{ formatTime(when(a)) }}</td>
                      <td data-label="Modo">{{ modeLabel(a.mode) }}</td>
                      <td data-label="Resultado">
                        <span class="score">
                          <span class="bar"><span [style.width.%]="a.percent" [attr.data-tone]="tone(a.percent)"></span></span>
                          <b>{{ round(a.percent) }}%</b>
                        </span>
                      </td>
                      <td data-label="Duración">{{ duration(a) }}</td>
                      <td data-label="Estado">
                        <ui-badge [tone]="a.passed ? 'success' : 'danger'">
                          {{ a.passed ? 'Aprobado' : 'No aprobado' }}
                        </ui-badge>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </article>
      }
    </div>
  `,
  styles: [`
    :host { display: grid; gap: 1rem; }
    .summary {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 0.75rem;
    }
    .sum {
      display: grid;
      gap: 0.15rem;
      padding: 0.85rem 1rem;
      border-radius: 1rem;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
    }
    .sum strong { font-size: 1.5rem; font-weight: 800; letter-spacing: -0.02em; }
    .sum span { color: var(--color-text-secondary); font-size: var(--text-xs); font-weight: 600; }
    .toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.55rem;
    }
    .grow { flex: 1; }
    .search, select {
      min-height: 2.5rem;
      padding: 0.45rem 0.75rem;
      border-radius: 0.7rem;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
      font-size: var(--text-sm);
    }
    .search { flex: 1 1 14rem; min-width: 12rem; }
    .none { margin: 0; color: var(--color-text-secondary); }
    .list { display: grid; gap: 0.6rem; }
    .student {
      border-radius: 1rem;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      overflow: hidden;
      transition: border-color 0.15s ease;
    }
    .student.open { border-color: color-mix(in srgb, var(--color-primary) 45%, var(--color-border)); }
    .student-head {
      width: 100%;
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) repeat(4, 5.5rem) auto;
      align-items: center;
      gap: 0.9rem;
      padding: 0.8rem 1rem;
      border: 0;
      background: transparent;
      color: inherit;
      font: inherit;
      text-align: left;
      cursor: pointer;
    }
    .student-head:hover { background: color-mix(in srgb, var(--color-primary) 5%, transparent); }
    .avatar {
      display: grid;
      place-items: center;
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 50%;
      font-size: 0.8rem;
      font-weight: 800;
      color: #062231;
      background: linear-gradient(135deg, #4eb6d4, #22c55e);
    }
    .who { display: grid; gap: 0.1rem; min-width: 0; }
    .who strong { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .who small, .metric small { color: var(--color-text-secondary); font-size: var(--text-xs); }
    .metric { display: grid; justify-items: center; gap: 0.05rem; }
    .metric strong { font-size: 1rem; font-weight: 800; }
    [data-tone='good'] { color: var(--color-success); }
    [data-tone='mid'] { color: var(--color-warning); }
    [data-tone='low'] { color: var(--color-danger); }
    .chev {
      font-size: 1.4rem;
      color: var(--color-text-secondary);
      transition: transform 0.15s ease;
    }
    .student.open .chev { transform: rotate(90deg); }
    .table-wrap { border-top: 1px solid var(--color-border); overflow-x: auto; }
    table.data { width: 100%; }
    .score { display: inline-flex; align-items: center; gap: 0.55rem; }
    .bar {
      width: 5.5rem;
      height: 0.4rem;
      border-radius: 999px;
      background: var(--color-chip);
      overflow: hidden;
    }
    .bar span { display: block; height: 100%; border-radius: inherit; background: var(--color-primary); }
    .bar span[data-tone='good'] { background: var(--color-success); }
    .bar span[data-tone='mid'] { background: var(--color-warning); }
    .bar span[data-tone='low'] { background: var(--color-danger); }
    @media (max-width: 900px) {
      .summary { grid-template-columns: repeat(2, minmax(0, 1fr)); }
      .student-head { grid-template-columns: auto minmax(0, 1fr) repeat(2, 4.5rem) auto; }
      .student-head .metric:nth-of-type(5),
      .student-head .metric:nth-of-type(6) { display: none; }
    }
    @media (max-width: 520px) {
      .student-head { grid-template-columns: auto minmax(0, 1fr) auto; }
      .student-head .metric { display: none; }
    }
  `]
})
export class UiResultsByStudentComponent {
  @Input({ required: true }) set items(value: ResultAttempt[]) {
    this.rows.set(value ?? []);
  }
  @Input() csvName = 'resultados.csv';

  private readonly rows = signal<ResultAttempt[]>([]);
  readonly query = signal('');
  readonly mode = signal<string>('all');
  readonly status = signal<StatusFilter>('all');
  readonly sort = signal<SortKey>('recent');
  private readonly open = signal<Set<number>>(new Set());

  private readonly filtered = computed(() => {
    const q = this.normalize(this.query());
    const mode = this.mode();
    const status = this.status();
    return this.rows().filter((r) =>
      (!q || this.normalize(r.userName).includes(q))
      && (mode === 'all' || (r.mode || '').toLowerCase() === mode)
      && (status === 'all' || (status === 'passed' ? r.passed : !r.passed))
    );
  });

  readonly groups = computed<StudentGroup[]>(() => {
    const map = new Map<number, ResultAttempt[]>();
    for (const r of this.filtered()) {
      const list = map.get(r.userId) ?? [];
      list.push(r);
      map.set(r.userId, list);
    }
    const groups = [...map.entries()].map(([userId, attempts]) => {
      attempts.sort((a, b) => this.when(b) - this.when(a));
      const sum = attempts.reduce((acc, a) => acc + Number(a.percent), 0);
      return {
        userId,
        name: attempts[0].userName,
        attempts,
        average: this.round(sum / attempts.length),
        best: this.round(Math.max(...attempts.map((a) => Number(a.percent)))),
        passedCount: attempts.filter((a) => a.passed).length,
        lastAt: this.when(attempts[0])
      };
    });
    const collator = new Intl.Collator('es', { sensitivity: 'base' });
    switch (this.sort()) {
      case 'name':
        return groups.sort((a, b) => collator.compare(a.name, b.name));
      case 'average':
        return groups.sort((a, b) => b.average - a.average);
      case 'attempts':
        return groups.sort((a, b) => b.attempts.length - a.attempts.length);
      default:
        return groups.sort((a, b) => b.lastAt - a.lastAt);
    }
  });

  readonly stats = computed(() => {
    const rows = this.filtered();
    const n = rows.length;
    return {
      students: new Set(rows.map((r) => r.userId)).size,
      attempts: n,
      average: n ? this.round(rows.reduce((a, r) => a + Number(r.percent), 0) / n) : 0,
      passRate: n ? this.round((rows.filter((r) => r.passed).length / n) * 100) : 0
    };
  });

  readonly allOpen = computed(() => {
    const groups = this.groups();
    return groups.length > 0 && groups.every((g) => this.open().has(g.userId));
  });

  isOpen(userId: number): boolean {
    return this.open().has(userId);
  }

  toggle(userId: number): void {
    const next = new Set(this.open());
    if (next.has(userId)) {
      next.delete(userId);
    } else {
      next.add(userId);
    }
    this.open.set(next);
  }

  toggleAll(): void {
    this.open.set(this.allOpen() ? new Set() : new Set(this.groups().map((g) => g.userId)));
  }

  when(a: ResultAttempt): number {
    return new Date(a.finishedAt || a.startedAt).getTime();
  }

  formatDate(ms: number): string {
    return Number.isFinite(ms) ? dateFmt.format(ms) : '—';
  }

  formatTime(ms: number): string {
    return Number.isFinite(ms) ? timeFmt.format(ms) : '—';
  }

  duration(a: ResultAttempt): string {
    if (!a.finishedAt) {
      return '—';
    }
    const secs = Math.max(0, Math.round((this.when(a) - new Date(a.startedAt).getTime()) / 1000));
    const h = Math.floor(secs / 3600);
    const m = Math.floor((secs % 3600) / 60);
    const s = secs % 60;
    if (h > 0) {
      return `${h} h ${m} min`;
    }
    return m > 0 ? `${m} min ${s} s` : `${s} s`;
  }

  modeLabel(mode: string): string {
    const key = (mode || '').toLowerCase();
    if (key === 'exam') return 'Examen';
    if (key === 'mixed_practice') return 'Simulacro personalizado';
    if (key === 'practice') return 'Práctica';
    return mode || 'Evaluación';
  }

  tone(percent: number): 'good' | 'mid' | 'low' {
    return percent >= 90 ? 'good' : percent >= 70 ? 'mid' : 'low';
  }

  round(v: number): number {
    return Math.round(Number(v) * 10) / 10;
  }

  initials(name: string): string {
    const parts = (name || '?').trim().split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '?') + (parts[1]?.[0] ?? '')).toUpperCase();
  }

  exportCsv(): void {
    const esc = (v: string | number) => {
      const s = String(v);
      return /[",\n;]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
    };
    const lines = [['Estudiante', 'Fecha', 'Hora', 'Modo', 'Porcentaje', 'Duración', 'Estado'].join(',')];
    for (const g of this.groups()) {
      for (const a of g.attempts) {
        lines.push([
          esc(g.name),
          esc(this.formatDate(this.when(a))),
          esc(this.formatTime(this.when(a))),
          esc(this.modeLabel(a.mode)),
          esc(this.round(a.percent)),
          esc(this.duration(a)),
          esc(a.passed ? 'Aprobado' : 'No aprobado')
        ].join(','));
      }
    }
    const blob = new Blob(['\uFEFF' + lines.join('\n')], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = this.csvName;
    link.click();
    URL.revokeObjectURL(url);
  }

  private normalize(s: string): string {
    return (s || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
  }
}
