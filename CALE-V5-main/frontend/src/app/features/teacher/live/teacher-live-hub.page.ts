import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { SessionStore } from '../../../core/auth/session.store';
import { AuthApi } from '../../auth/api/auth.api';
import { LiveApi } from '../../live/api/live.api';
import { BankAdminDto, BankThemeDto, TeacherApi } from '../api/teacher.api';

@Component({
  selector: 'app-teacher-live-hub-page',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, UiButtonComponent, UiErrorComponent],
  template: `
    <section class="page">
      <header class="hero">
        <p class="eyebrow">CALE LIVE</p>
        <h1>Aula en Vivo</h1>
        <p class="lead">
          Arma tu simulacro: elige bancos, combínalos como quieras y define cuántas preguntas salen.
        </p>
      </header>

      @if (accessHint(); as hint) {
        <div class="access-banner" [class.warn]="hint.kind === 'school'">
          <p>{{ hint.message }}</p>
          @if (hint.link) {
            <a [routerLink]="hint.link">Ir a {{ hint.linkLabel }}</a>
          }
        </div>
      }

      <ui-error [message]="error()" />

      <div class="modes">
        <article class="card active">
          <h2>Configurar simulacro</h2>
          <form [formGroup]="form" (ngSubmit)="create()">
            <label class="field">
              Título de la actividad
              <input formControlName="title" />
            </label>

            <div class="field">
              <div class="field-head">
                <span>Bancos de preguntas</span>
                <div class="field-actions">
                  <button type="button" class="linkish" (click)="selectAllBanks()">Todos</button>
                  <button type="button" class="linkish" (click)="clearBanks()">Ninguno</button>
                </div>
              </div>
              <p class="field-hint">Marca uno o varios bancos. Las preguntas se mezclan del total seleccionado.</p>
              @if (!banks().length) {
                <p class="bank-empty">Cargando bancos…</p>
              } @else {
                <ul class="bank-list">
                  @for (bank of banks(); track bank.id) {
                    <li class="bank-card" [class.selected]="isBankSelected(bank.id)">
                      <label class="bank-item">
                        <input
                          type="checkbox"
                          [checked]="isBankSelected(bank.id)"
                          (change)="toggleBank(bank.id)" />
                        <span class="bank-name">{{ bank.name }}</span>
                        <span class="bank-meta">{{ bank.questionCount }} preg.</span>
                      </label>
                      @if (isBankSelected(bank.id) && bankThemes(bank).length) {
                        <details class="theme-details">
                          <summary>Filtrar temas (opcional)</summary>
                          <div class="theme-panel">
                            <div class="theme-toolbar">
                              <span>{{ bank.themeLabel || 'Temas' }}</span>
                              <button type="button" class="linkish" (click)="clearBankThemes(bank.id); $event.preventDefault()">
                                Usar todo el banco
                              </button>
                            </div>
                            @if (bankThemes(bank).length > 8) {
                              <input
                                class="theme-search"
                                type="search"
                                [value]="themeQuery()"
                                (input)="themeQuery.set($any($event.target).value)"
                                placeholder="Buscar tema…" />
                            }
                            <ul class="theme-list">
                              @for (theme of visibleThemes(bank); track theme.name) {
                                <li>
                                  <label class="theme-item">
                                    <input
                                      type="checkbox"
                                      [checked]="isThemeSelected(bank.id, theme.name)"
                                      (change)="toggleTheme(bank.id, theme.name)" />
                                    <span>{{ theme.name }}</span>
                                    <span class="bank-meta">{{ theme.questionCount }}</span>
                                  </label>
                                </li>
                              }
                            </ul>
                            <p class="bank-summary">
                              @if (bankThemeSelection(bank.id).length === 0) {
                                Todo el banco: {{ bank.questionCount }} preguntas
                              } @else {
                                {{ bankThemeSelection(bank.id).length }} tema(s) · {{ bankSelectedCount(bank) }} preguntas
                              }
                            </p>
                          </div>
                        </details>
                      }
                    </li>
                  }
                </ul>
                <p class="pool-summary">
                  <strong>{{ selectedBankIds().length }}</strong> banco(s) ·
                  <strong>{{ selectedQuestionTotal() }}</strong> preguntas en el pool
                </p>
              }
            </div>

            <div class="grid">
              <label class="field">
                Cantidad de preguntas
                <input
                  type="number"
                  formControlName="questionCount"
                  [attr.max]="maxQuestions()"
                  min="1" />
                <span class="field-hint">Máximo {{ maxQuestions() }} con la selección actual</span>
              </label>
              <label class="field">
                Segundos por pregunta
                <input type="number" formControlName="secondsPerQuestion" min="5" max="600" />
              </label>
            </div>
            <div class="presets">
              <span>Atajos:</span>
              <button type="button" class="chip" (click)="applyPreset(10, 30)">10 preg · 30 s</button>
              <button type="button" class="chip" (click)="applyPreset(25, 72)">25 preg · 72 s (CALE)</button>
              <button type="button" class="chip" (click)="applyPreset(maxQuestions(), 60)">Usar todas ({{ maxQuestions() }})</button>
            </div>

            <label class="field">
              Modo
              <select formControlName="mode">
                <option value="Exam">Examen (sin revelar durante)</option>
                <option value="Competitive">Competitivo (Top 5 + puntos)</option>
                <option value="Pedagogical">Pedagógico (dudas + feedback)</option>
              </select>
            </label>

            <ui-button type="submit" [loading]="loading()" [disabled]="!!accessHint()">
              Crear sala y proyectar
            </ui-button>
          </form>
        </article>

        <article class="card tip">
          <h2>En el proyector</h2>
          <ul>
            <li><strong>Combina bancos:</strong> Normas + Señales, o solo uno.</li>
            <li><strong>Tú eliges cuántas:</strong> de 1 hasta el total disponible.</li>
            <li><strong>Temas opcionales:</strong> para enfocar la clase en un subtema.</li>
          </ul>
        </article>
      </div>

      <p class="hint">
        ¿Los estudiantes ya tienen el código?
        <a routerLink="/live/join">Ir a unirse</a>
      </p>
    </section>
  `,
  styles: `
    .page { padding: var(--page-pad); max-width: 960px; margin: 0 auto; }
    .eyebrow { color: var(--color-primary); font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; font-size: var(--text-xs); }
    .lead { color: var(--color-text-secondary); }
    .access-banner {
      margin: 1rem 0;
      padding: 0.85rem 1rem;
      border-radius: var(--radius-md);
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      font-size: var(--text-sm);
    }
    .access-banner.warn {
      border-color: color-mix(in srgb, var(--color-warning, #f0b429) 55%, var(--color-border));
      background: color-mix(in srgb, var(--color-warning, #f0b429) 12%, var(--color-surface));
    }
    .access-banner p { margin: 0 0 0.5rem; }
    .access-banner a { color: var(--color-primary); font-weight: 700; }
    .modes { display: grid; gap: 1rem; margin-top: 1.25rem; }
    .card { border: 1px solid var(--color-border); border-radius: var(--radius-lg); padding: 1.1rem; background: var(--color-surface); }
    .card.tip ul { margin: 0.5rem 0 0; padding-left: 1.1rem; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .card.tip li { margin: 0.35rem 0; }
    .field { display: grid; gap: 0.35rem; margin: 0.75rem 0; font-size: var(--text-sm); }
    .field-head { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; }
    .field-actions { display: flex; gap: 0.65rem; }
    .field-hint { margin: 0; color: var(--color-text-secondary); font-size: var(--text-xs); }
    .field input, .field select { padding: 0.65rem 0.8rem; border-radius: var(--radius-md); border: 1px solid var(--color-border); background: var(--color-background); color: var(--color-text); }
    .bank-list { list-style: none; margin: 0.35rem 0 0; padding: 0; display: grid; gap: 0.65rem; }
    .bank-card { border-radius: var(--radius-md); border: 1px solid var(--color-border); background: var(--color-background); }
    .bank-card.selected { border-color: color-mix(in srgb, var(--color-primary) 55%, var(--color-border)); }
    .bank-item {
      display: grid;
      grid-template-columns: auto 1fr auto;
      gap: 0.55rem;
      align-items: center;
      padding: 0.65rem 0.75rem;
      cursor: pointer;
      font-size: var(--text-sm);
    }
    .bank-card.selected .bank-item { background: color-mix(in srgb, var(--color-primary) 8%, var(--color-background)); }
    .bank-name { font-weight: 700; }
    .bank-meta { color: var(--color-text-secondary); font-size: var(--text-xs); }
    .theme-details { padding: 0 0.75rem 0.5rem; font-size: var(--text-sm); }
    .theme-details summary { cursor: pointer; color: var(--color-primary); font-weight: 600; padding: 0.25rem 0; }
    .theme-panel { display: grid; gap: 0.45rem; margin-top: 0.35rem; }
    .theme-toolbar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      color: var(--color-text-secondary);
      font-size: var(--text-xs);
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .linkish { border: 0; background: none; color: var(--color-primary); font-weight: 700; cursor: pointer; padding: 0; font-size: var(--text-xs); }
    .theme-search {
      padding: 0.45rem 0.65rem;
      border-radius: var(--radius-md);
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
    }
    .theme-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.3rem; max-height: 200px; overflow: auto; }
    .theme-item {
      display: grid;
      grid-template-columns: auto 1fr auto;
      gap: 0.45rem;
      align-items: center;
      padding: 0.4rem 0.5rem;
      border-radius: 8px;
      font-size: var(--text-sm);
      cursor: pointer;
    }
    .theme-item:hover { background: color-mix(in srgb, var(--color-primary) 8%, transparent); }
    .bank-summary, .pool-summary { margin: 0.35rem 0 0; color: var(--color-text-secondary); font-size: var(--text-xs); }
    .pool-summary strong { color: var(--color-text); }
    .bank-empty { margin: 0.35rem 0 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .presets { display: flex; flex-wrap: wrap; gap: 0.45rem; align-items: center; margin: 0.5rem 0 0.75rem; font-size: var(--text-xs); color: var(--color-text-secondary); }
    .chip {
      border: 1px solid var(--color-border);
      background: var(--color-background);
      color: var(--color-text);
      border-radius: 999px;
      padding: 0.3rem 0.7rem;
      font-size: var(--text-xs);
      cursor: pointer;
    }
    .chip:hover { border-color: var(--color-primary); color: var(--color-primary); }
    .hint { margin-top: 1.5rem; color: var(--color-text-secondary); }
    @media (max-width: 640px) { .grid { grid-template-columns: 1fr; } }
  `
})
export class TeacherLiveHubPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(LiveApi);
  private readonly teacherApi = inject(TeacherApi);
  private readonly authApi = inject(AuthApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly contextReady = signal(false);
  readonly banks = signal<BankAdminDto[]>([]);
  readonly selectedBankIds = signal<number[]>([]);
  readonly selectedThemesByBank = signal<Record<number, string[]>>({});
  readonly themeQuery = signal('');

  readonly maxQuestions = computed(() => {
    const total = this.selectedQuestionTotal();
    return Math.min(Math.max(total, 1), 100);
  });

  readonly accessHint = computed(() => {
    if (!this.contextReady()) {
      return null;
    }
    const user = this.session.user();
    if (!user || user.role === 'Admin') {
      return null;
    }
    if (user.role === 'School') {
      if (!user.isMembershipActive) {
        return {
          kind: 'membership' as const,
          message: 'Tu escuela necesita un plan activo para usar Aula en Vivo.',
          link: '/school/membership',
          linkLabel: 'membresía'
        };
      }
      return null;
    }
    if (user.role === 'Teacher') {
      if (!user.schoolId) {
        return {
          kind: 'school' as const,
          message: 'Aún no estás vinculado a una escuela. Solicita unirte con el NIT o correo de la escuela.',
          link: '/profile',
          linkLabel: 'tu perfil'
        };
      }
      if (!user.isMembershipActive) {
        return {
          kind: 'membership' as const,
          message: 'Tu escuela no tiene plan activo. Pide a la escuela que active la membresía.',
          link: '/profile',
          linkLabel: 'tu perfil'
        };
      }
    }
    return null;
  });

  readonly form = this.fb.nonNullable.group({
    title: ['CALE Aula en Vivo'],
    mode: ['Exam', Validators.required],
    questionCount: [10, [Validators.required, Validators.min(1), Validators.max(100)]],
    secondsPerQuestion: [30, [Validators.required, Validators.min(5), Validators.max(600)]]
  });

  ngOnInit(): void {
    this.loadBanks();
    this.authApi.me().subscribe({
      next: (dto) => {
        this.session.patchUser({
          id: dto.id,
          name: dto.name,
          email: dto.email,
          role: dto.role,
          mustChangePassword: !!dto.mustChangePassword
        });
        this.session.applySchoolContext(dto.school ?? null);
        this.contextReady.set(true);
      },
      error: () => this.contextReady.set(true)
    });
  }

  isBankSelected(id: number): boolean {
    return this.selectedBankIds().includes(id);
  }

  bankThemes(bank: BankAdminDto): BankThemeDto[] {
    return bank.themes ?? [];
  }

  visibleThemes(bank: BankAdminDto): BankThemeDto[] {
    const q = this.themeQuery().trim().toLowerCase();
    const themes = this.bankThemes(bank);
    if (!q) {
      return themes;
    }
    return themes.filter((t) => t.name.toLowerCase().includes(q));
  }

  bankThemeSelection(bankId: number): string[] {
    return this.selectedThemesByBank()[bankId] ?? [];
  }

  isThemeSelected(bankId: number, theme: string): boolean {
    const selected = this.bankThemeSelection(bankId);
    return selected.length === 0 || selected.includes(theme);
  }

  selectAllBanks(): void {
    const ids = this.banks().map((b) => b.id);
    this.selectedBankIds.set(ids);
    const themes: Record<number, string[]> = {};
    for (const id of ids) {
      themes[id] = this.bankThemeSelection(id);
    }
    this.selectedThemesByBank.set(themes);
  }

  clearBanks(): void {
    this.selectedBankIds.set([]);
    this.selectedThemesByBank.set({});
  }

  toggleBank(id: number): void {
    const selected = this.isBankSelected(id);
    this.selectedBankIds.update((ids) =>
      selected ? ids.filter((x) => x !== id) : [...ids, id]
    );
    this.selectedThemesByBank.update((map) => {
      const next = { ...map };
      if (selected) {
        delete next[id];
      } else {
        next[id] = [];
      }
      return next;
    });
  }

  toggleTheme(bankId: number, theme: string): void {
    if (!this.isBankSelected(bankId)) {
      this.toggleBank(bankId);
    }
    this.selectedThemesByBank.update((map) => {
      const bank = this.banks().find((b) => b.id === bankId);
      const allNames = (bank?.themes ?? []).map((t) => t.name);
      const current = map[bankId] ?? [];
      const specific = current.length === 0 ? allNames : current;
      const nextThemes = specific.includes(theme)
        ? specific.filter((t) => t !== theme)
        : [...specific, theme];
      return {
        ...map,
        [bankId]: nextThemes.length === allNames.length ? [] : nextThemes
      };
    });
  }

  clearBankThemes(bankId: number): void {
    this.selectedThemesByBank.update((map) => ({ ...map, [bankId]: [] }));
  }

  bankSelectedCount(bank: BankAdminDto): number {
    const selected = this.bankThemeSelection(bank.id);
    if (selected.length === 0) {
      return bank.questionCount;
    }
    const wanted = new Set(selected);
    return this.bankThemes(bank)
      .filter((t) => wanted.has(t.name))
      .reduce((sum, t) => sum + t.questionCount, 0);
  }

  selectedQuestionTotal(): number {
    const selected = new Set(this.selectedBankIds());
    return this.banks()
      .filter((b) => selected.has(b.id))
      .reduce((sum, b) => sum + this.bankSelectedCount(b), 0);
  }

  applyPreset(count: number, seconds: number): void {
    const max = this.maxQuestions();
    if (max < 1) {
      this.error.set('Elige al menos un banco con preguntas.');
      return;
    }
    this.form.patchValue({
      questionCount: Math.min(count, max),
      secondsPerQuestion: seconds
    });
    this.error.set(null);
  }

  private loadBanks(): void {
    this.teacherApi.banks(true, true).subscribe({
      next: (items) => {
        const active = items.filter((b) => b.isActive && b.questionCount > 0);
        this.banks.set(active);
      },
      error: () => this.banks.set([])
    });
  }

  create(): void {
    if (this.accessHint()) {
      return;
    }
    const bankIds = this.selectedBankIds();
    if (bankIds.length === 0) {
      this.error.set('Elige al menos un banco de preguntas.');
      return;
    }
    const v = this.form.getRawValue();
    const available = this.selectedQuestionTotal();
    const needed = Math.min(v.questionCount, 100);
    if (available < needed) {
      this.error.set(
        `Tu selección tiene ${available} preguntas; pediste ${needed}. Reduce la cantidad o agrega más bancos.`
      );
      return;
    }
    const bankTopicFilters: Record<number, string[]> = {};
    for (const id of bankIds) {
      bankTopicFilters[id] = this.bankThemeSelection(id);
    }
    this.error.set(null);
    this.loading.set(true);
    this.api.create({
      title: v.title,
      mode: v.mode,
      bankIds,
      config: {
        caleStandardPreset: false,
        questionCount: needed,
        secondsPerQuestion: v.secondsPerQuestion,
        randomize: true,
        shuffleOptions: true,
        showRanking: v.mode === 'Competitive',
        anonymousNames: false,
        feedbackTiming: v.mode === 'Exam' ? 'end' : 'immediate',
        bankIds,
        bankTopicFilters
      }
    }).subscribe({
      next: (lobby) => {
        this.loading.set(false);
        void this.router.navigate(['/teacher/live', lobby.sessionId, 'host']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
