import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { SessionStore } from '../../../core/auth/session.store';
import { AuthApi } from '../../auth/api/auth.api';
import { LiveApi } from '../../live/api/live.api';
import { BankAdminDto, BankThemeDto, TeacherApi } from '../api/teacher.api';
import {
  LiveDistributionMode,
  LiveHubDraft,
  LiveHubPreset,
  deleteLiveHubPreset,
  loadLiveHubDraft,
  loadLiveHubPresets,
  saveLiveHubDraft,
  saveLiveHubPreset
} from './live-hub-storage';

@Component({
  selector: 'app-teacher-live-hub-page',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, UiButtonComponent, UiErrorComponent],
  templateUrl: './teacher-live-hub.page.html',
  styleUrl: './teacher-live-hub.page.css'
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
  readonly bankQuotas = signal<Record<number, number>>({});
  readonly themeQueryByBank = signal<Record<number, string>>({});
  readonly selectedDifficulties = signal<string[]>([]);
  readonly distributionMode = signal<LiveDistributionMode>('mix');
  readonly savedPresets = signal<LiveHubPreset[]>(loadLiveHubPresets());
  readonly presetName = signal('');

  readonly form = this.fb.nonNullable.group({
    title: ['CALE Aula en Vivo'],
    mode: ['Exam', Validators.required],
    questionCount: [10, [Validators.required, Validators.min(1), Validators.max(100)]],
    secondsPerQuestion: [30, [Validators.required, Validators.min(5), Validators.max(600)]],
    randomize: [true],
    shuffleOptions: [true]
  });

  readonly maxQuestions = computed(() => {
    const total = this.selectedQuestionTotal();
    return Math.min(Math.max(total, 1), 100);
  });

  readonly availableDifficulties = computed(() => {
    const selected = new Set(this.selectedBankIds());
    const names = new Map<string, number>();
    for (const bank of this.banks()) {
      if (!selected.has(bank.id)) {
        continue;
      }
      for (const d of bank.difficulties ?? []) {
        names.set(d.name, (names.get(d.name) ?? 0) + d.questionCount);
      }
    }
    return [...names.entries()]
      .map(([name, questionCount]) => ({ name, questionCount }))
      .sort((a, b) => a.name.localeCompare(b.name, 'es'));
  });

  readonly quotaTotal = computed(() =>
    Object.values(this.bankQuotas()).reduce((sum, n) => sum + (n || 0), 0)
  );

  readonly configPreview = computed(() => {
    const bankNames = this.banks()
      .filter((b) => this.selectedBankIds().includes(b.id))
      .map((b) => b.name);
    const themed = Object.entries(this.selectedThemesByBank())
      .filter(([, themes]) => themes.length > 0)
      .length;
    const diffs = this.selectedDifficulties();
    const mode = this.distributionMode();
    const v = this.form.getRawValue();
    return {
      banks: bankNames,
      pool: this.selectedQuestionTotal(),
      questions: v.questionCount,
      seconds: v.secondsPerQuestion,
      themedBanks: themed,
      difficulties: diffs,
      distribution: mode,
      quotaTotal: this.quotaTotal()
    };
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

  constructor() {
    effect(() => {
      const draft: LiveHubDraft = {
        ...this.form.getRawValue(),
        distributionMode: this.distributionMode(),
        selectedBankIds: this.selectedBankIds(),
        selectedThemesByBank: this.selectedThemesByBank(),
        bankQuotas: this.bankQuotas(),
        selectedDifficulties: this.selectedDifficulties()
      };
      saveLiveHubDraft(draft);
    });
  }

  ngOnInit(): void {
    this.restoreDraft();
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

  themeQueryFor(bankId: number): string {
    return this.themeQueryByBank()[bankId] ?? '';
  }

  setThemeQuery(bankId: number, value: string): void {
    this.themeQueryByBank.update((map) => ({ ...map, [bankId]: value }));
  }

  visibleThemes(bank: BankAdminDto): BankThemeDto[] {
    const q = this.themeQueryFor(bank.id).trim().toLowerCase();
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
    this.syncQuotasForSelection(ids);
  }

  clearBanks(): void {
    this.selectedBankIds.set([]);
    this.selectedThemesByBank.set({});
    this.bankQuotas.set({});
  }

  toggleBank(id: number): void {
    const selected = this.isBankSelected(id);
    const nextIds = selected
      ? this.selectedBankIds().filter((x) => x !== id)
      : [...this.selectedBankIds(), id];
    this.selectedBankIds.set(nextIds);
    this.selectedThemesByBank.update((map) => {
      const next = { ...map };
      if (selected) {
        delete next[id];
      } else {
        next[id] = [];
      }
      return next;
    });
    this.syncQuotasForSelection(nextIds);
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

  selectAllThemes(bankId: number): void {
    this.selectedThemesByBank.update((map) => ({ ...map, [bankId]: [] }));
  }

  clearBankThemes(bankId: number): void {
    this.selectedThemesByBank.update((map) => ({ ...map, [bankId]: [] }));
  }

  bankQuota(bankId: number): number {
    return this.bankQuotas()[bankId] ?? 0;
  }

  setBankQuota(bankId: number, value: number): void {
    const safe = Math.max(0, Math.min(100, Number(value) || 0));
    this.bankQuotas.update((map) => ({ ...map, [bankId]: safe }));
    if (!this.isBankSelected(bankId) && safe > 0) {
      this.toggleBank(bankId);
    }
  }

  setDistributionMode(mode: LiveDistributionMode): void {
    this.distributionMode.set(mode);
    if (mode === 'quotas') {
      this.syncQuotasEvenly();
    }
  }

  toggleDifficulty(name: string): void {
    this.selectedDifficulties.update((items) =>
      items.includes(name) ? items.filter((d) => d !== name) : [...items, name]
    );
  }

  clearDifficulties(): void {
    this.selectedDifficulties.set([]);
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

  saveCurrentPreset(): void {
    const name = this.presetName().trim();
    if (!name) {
      this.error.set('Escribe un nombre para guardar la configuración.');
      return;
    }
    const preset: LiveHubPreset = {
      id: crypto.randomUUID(),
      name,
      savedAt: new Date().toISOString(),
      ...this.buildDraft()
    };
    saveLiveHubPreset(preset);
    this.savedPresets.set(loadLiveHubPresets());
    this.presetName.set('');
    this.error.set(null);
  }

  loadPreset(preset: LiveHubPreset): void {
    this.form.patchValue({
      title: preset.title,
      mode: preset.mode,
      questionCount: preset.questionCount,
      secondsPerQuestion: preset.secondsPerQuestion,
      randomize: preset.randomize,
      shuffleOptions: preset.shuffleOptions
    });
    this.distributionMode.set(preset.distributionMode);
    this.selectedBankIds.set([...preset.selectedBankIds]);
    this.selectedThemesByBank.set({ ...preset.selectedThemesByBank });
    this.bankQuotas.set({ ...preset.bankQuotas });
    this.selectedDifficulties.set([...preset.selectedDifficulties]);
    this.error.set(null);
  }

  removePreset(id: string): void {
    deleteLiveHubPreset(id);
    this.savedPresets.set(loadLiveHubPresets());
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
    const diffs = new Set(this.selectedDifficulties());
    let total = 0;
    for (const bank of this.banks()) {
      if (!selected.has(bank.id)) {
        continue;
      }
      let count = this.bankSelectedCount(bank);
      if (diffs.size > 0) {
        count = (bank.difficulties ?? [])
          .filter((d) => diffs.has(d.name))
          .reduce((sum, d) => sum + d.questionCount, 0);
        const themed = this.bankThemeSelection(bank.id);
        if (themed.length > 0) {
          count = Math.min(count, this.bankSelectedCount(bank));
        }
      }
      total += count;
    }
    return total;
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
        `Tu selección tiene ${available} preguntas; pediste ${needed}. Reduce la cantidad o amplía bancos/temas.`
      );
      return;
    }

    const bankTopicFilters: Record<number, string[]> = {};
    for (const id of bankIds) {
      bankTopicFilters[id] = this.bankThemeSelection(id);
    }

    let bankQuestionQuotas: Record<number, number> | undefined;
    if (this.distributionMode() === 'quotas') {
      const quotas: Record<number, number> = {};
      let sum = 0;
      for (const id of bankIds) {
        const q = this.bankQuota(id);
        if (q > 0) {
          quotas[id] = q;
          sum += q;
        }
      }
      if (sum > needed) {
        this.error.set(`Los cupos por banco suman ${sum}, pero solo pediste ${needed} preguntas.`);
        return;
      }
      if (sum > 0) {
        bankQuestionQuotas = quotas;
      }
    }

    const difficulties = this.selectedDifficulties();
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
        randomize: v.randomize,
        shuffleOptions: v.shuffleOptions,
        showRanking: v.mode === 'Competitive',
        anonymousNames: false,
        feedbackTiming: v.mode === 'Exam' ? 'end' : 'immediate',
        bankIds,
        bankTopicFilters,
        bankQuestionQuotas,
        difficultyFilters: difficulties.length ? difficulties : undefined
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

  private restoreDraft(): void {
    const draft = loadLiveHubDraft();
    if (!draft) {
      return;
    }
    this.form.patchValue({
      title: draft.title,
      mode: draft.mode,
      questionCount: draft.questionCount,
      secondsPerQuestion: draft.secondsPerQuestion,
      randomize: draft.randomize,
      shuffleOptions: draft.shuffleOptions
    });
    this.distributionMode.set(draft.distributionMode ?? 'mix');
    this.selectedBankIds.set(draft.selectedBankIds ?? []);
    this.selectedThemesByBank.set(draft.selectedThemesByBank ?? {});
    this.bankQuotas.set(draft.bankQuotas ?? {});
    this.selectedDifficulties.set(draft.selectedDifficulties ?? []);
  }

  private buildDraft(): LiveHubDraft {
    return {
      ...this.form.getRawValue(),
      distributionMode: this.distributionMode(),
      selectedBankIds: this.selectedBankIds(),
      selectedThemesByBank: this.selectedThemesByBank(),
      bankQuotas: this.bankQuotas(),
      selectedDifficulties: this.selectedDifficulties()
    };
  }

  private loadBanks(): void {
    this.teacherApi.banks(true, true).subscribe({
      next: (items) => {
        const active = items.filter((b) => b.isActive && b.questionCount > 0);
        this.banks.set(active);
        if (this.selectedBankIds().length === 0 && active.length) {
          this.selectAllBanks();
        } else {
          this.syncQuotasForSelection(this.selectedBankIds());
        }
      },
      error: () => this.banks.set([])
    });
  }

  private syncQuotasForSelection(ids: number[]): void {
    this.bankQuotas.update((map) => {
      const next: Record<number, number> = {};
      for (const id of ids) {
        next[id] = map[id] ?? 0;
      }
      return next;
    });
  }

  private syncQuotasEvenly(): void {
    const ids = this.selectedBankIds();
    const total = Math.min(this.form.getRawValue().questionCount, this.maxQuestions());
    if (!ids.length || total <= 0) {
      return;
    }
    const base = Math.floor(total / ids.length);
    let remainder = total - base * ids.length;
    const next: Record<number, number> = {};
    for (const id of ids) {
      next[id] = base + (remainder > 0 ? 1 : 0);
      if (remainder > 0) {
        remainder--;
      }
    }
    this.bankQuotas.set(next);
  }
}
