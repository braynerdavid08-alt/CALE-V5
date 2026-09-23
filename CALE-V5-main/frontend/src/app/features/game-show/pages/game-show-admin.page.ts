import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  GameShowApi,
  GameShowPackSummaryDto,
  GameShowRoundInput,
  GameShowSettingsDto
} from '../api/game-show.api';

type AdminTab = 'questions' | 'times' | 'rules' | 'preview';

const emptySettings = (): GameShowSettingsDto => ({
  faceOffSeconds: 25,
  controlSeconds: 30,
  stealSeconds: 25,
  lightningSeconds: 45,
  roundTransitionSeconds: 3,
  drumrollMs: 1100,
  revealHighlightMs: 2000,
  strikeFlashMs: 1000,
  celebrationMs: 2600,
  correctFlashMs: 2500,
  scoreboardFlashMs: 2500,
  maxStrikes: 3,
  enableFaceOff: true,
  enableSteal: true,
  enableLightning: true,
  enableSounds: true,
  enableAnimations: true,
  allowPause: true,
  allowSkipRound: true,
  allowHostEndRound: true,
  enableAudienceVote: false,
  tieBreakMode: 'Host'
});

function blankRound(): GameShowRoundInput {
  return {
    questionText: '',
    category: '',
    isActive: true,
    answers: [
      { text: '', points: 30, isActive: true },
      { text: '', points: 25, isActive: true },
      { text: '', points: 20, isActive: true },
      { text: '', points: 15, isActive: true },
      { text: '', points: 10, isActive: true }
    ]
  };
}

@Component({
  selector: 'app-game-show-admin-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiPageHeaderComponent,
    UiSuccessComponent
  ],
  template: `
    <ui-page-header
      title="Configuración · 100 Estudiantes Dijeron"
      subtitle="Banco de preguntas, tiempos y reglas. Los cambios globales no afectan partidas ya creadas." />
    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />

    <nav class="tabs" role="tablist">
      @for (t of tabs; track t.id) {
        <button type="button" class="tab" [class.on]="tab() === t.id" (click)="tab.set(t.id)">{{ t.label }}</button>
      }
      <a routerLink="/teacher/game-show" class="back">← Volver al hub</a>
    </nav>

    @if (tab() === 'times' || tab() === 'rules') {
      <p class="dirty" [class.on]="settingsDirty()">
        {{ settingsDirty() ? 'Hay cambios sin guardar' : 'Sin cambios pendientes' }}
      </p>
      <div class="actions">
        <ui-button type="button" [loading]="savingSettings()" [disabled]="!settingsDirty()" (click)="saveSettings()">
          Guardar cambios
        </ui-button>
        <ui-button type="button" variant="secondary" (click)="reloadSettings()" [disabled]="!settingsDirty()">
          Cancelar
        </ui-button>
        <ui-button type="button" variant="ghost" (click)="restoreSettings()">Restaurar predeterminados</ui-button>
      </div>
    }

    @if (tab() === 'times') {
      <section class="panel grid2">
        <label class="field">Face-Off (s)
          <input class="input" type="number" min="3" max="120" [(ngModel)]="draft.faceOffSeconds" (ngModelChange)="markDirty()" />
        </label>
        <label class="field">Control / responder (s)
          <input class="input" type="number" min="3" max="180" [(ngModel)]="draft.controlSeconds" (ngModelChange)="markDirty()" />
        </label>
        <label class="field">Robo (s)
          <input class="input" type="number" min="3" max="120" [(ngModel)]="draft.stealSeconds" (ngModelChange)="markDirty()" />
        </label>
        <label class="field">Relámpago final (s)
          <input class="input" type="number" min="5" max="300" [(ngModel)]="draft.lightningSeconds" (ngModelChange)="markDirty()" />
        </label>
        <label class="field">Transición de ronda (s)
          <input class="input" type="number" min="0" max="60" [(ngModel)]="draft.roundTransitionSeconds" (ngModelChange)="markDirty()" />
        </label>
        <label class="field">Drumroll (ms)
          <input class="input" type="number" min="0" max="10000" [(ngModel)]="draft.drumrollMs" (ngModelChange)="markDirty()" />
        </label>
        <label class="field">Resaltar revelación (ms)
          <input class="input" type="number" min="0" max="10000" [(ngModel)]="draft.revealHighlightMs" (ngModelChange)="markDirty()" />
        </label>
        <label class="field">Flash de X (ms)
          <input class="input" type="number" min="0" max="10000" [(ngModel)]="draft.strikeFlashMs" (ngModelChange)="markDirty()" />
        </label>
        <label class="field">Celebración / confeti (ms)
          <input class="input" type="number" min="0" max="15000" [(ngModel)]="draft.celebrationMs" (ngModelChange)="markDirty()" />
        </label>
        <label class="field">Flash acierto (ms)
          <input class="input" type="number" min="0" max="10000" [(ngModel)]="draft.correctFlashMs" (ngModelChange)="markDirty()" />
        </label>
        <label class="field">Flash marcador (ms)
          <input class="input" type="number" min="0" max="10000" [(ngModel)]="draft.scoreboardFlashMs" (ngModelChange)="markDirty()" />
        </label>
      </section>
    }

    @if (tab() === 'rules') {
      <section class="panel grid2">
        <label class="field">Máximo de X
          <input class="input" type="number" min="1" max="5" [(ngModel)]="draft.maxStrikes" (ngModelChange)="markDirty()" />
        </label>
        <label class="field">Empate (reservado)
          <select class="input" [(ngModel)]="draft.tieBreakMode" (ngModelChange)="markDirty()">
            <option value="Host">Host decide</option>
            <option value="Audience" disabled>Votación del público (próximamente)</option>
            <option value="Both" disabled>Ambos (próximamente)</option>
          </select>
        </label>
        @for (flag of ruleFlags; track flag.key) {
          <label class="check">
            <input type="checkbox" [(ngModel)]="$any(draft)[flag.key]" (ngModelChange)="markDirty()" [disabled]="flag.disabled" />
            {{ flag.label }}
            @if (flag.hint) { <span class="hint">{{ flag.hint }}</span> }
          </label>
        }
      </section>
    }

    @if (tab() === 'questions') {
      <section class="panel">
        <div class="row">
          <label class="field grow">Pack
            <select class="input" [ngModel]="selectedPackId()" (ngModelChange)="loadPack(+$event)">
              <option [ngValue]="0">— Nuevo pack —</option>
              @for (p of packs(); track p.id) {
                <option [ngValue]="p.id">{{ p.name }} ({{ p.roundCount }})</option>
              }
            </select>
          </label>
          <label class="field grow">Nombre del pack
            <input class="input" [(ngModel)]="packName" />
          </label>
        </div>
        <div class="row">
          <label class="field grow">Buscar
            <input class="input" [(ngModel)]="query" placeholder="Texto o categoría" />
          </label>
          <label class="field">Filtro
            <select class="input" [(ngModel)]="filterActive">
              <option value="all">Todas</option>
              <option value="on">Activas</option>
              <option value="off">Inactivas</option>
            </select>
          </label>
        </div>
        <div class="actions">
          <ui-button type="button" variant="secondary" (click)="addQuestion()">+ Pregunta</ui-button>
          <ui-button type="button" [loading]="savingPack()" (click)="savePack()">Guardar pack</ui-button>
        </div>

        @for (r of filteredRounds(); track $index; let i = $index) {
          <article class="round" [class.off]="r.isActive === false">
            <header>
              <strong>#{{ realIndex(r) + 1 }}</strong>
              <div class="hist-actions">
                <ui-button type="button" variant="ghost" (click)="moveRound(realIndex(r), -1)">↑</ui-button>
                <ui-button type="button" variant="ghost" (click)="moveRound(realIndex(r), 1)">↓</ui-button>
                <ui-button type="button" variant="ghost" (click)="duplicateRound(realIndex(r))">Duplicar</ui-button>
                <ui-button type="button" variant="ghost" (click)="previewIndex.set(realIndex(r)); tab.set('preview')">Vista previa</ui-button>
                <ui-button type="button" variant="ghost" (click)="removeRound(realIndex(r))">Eliminar</ui-button>
              </div>
            </header>
            <label class="check"><input type="checkbox" [(ngModel)]="r.isActive" /> Activa</label>
            <label class="field">Categoría
              <input class="input" [(ngModel)]="r.category" />
            </label>
            <label class="field">Pregunta
              <textarea class="input" rows="2" [(ngModel)]="r.questionText"></textarea>
            </label>
            @for (a of r.answers; track $index; let ai = $index) {
              <div class="ans">
                <label class="check"><input type="checkbox" [(ngModel)]="a.isActive" /></label>
                <input class="input" [(ngModel)]="a.text" placeholder="Respuesta" />
                <input class="input pts" type="number" min="1" [(ngModel)]="a.points" />
                <ui-button type="button" variant="ghost" (click)="r.answers.splice(ai, 1)">×</ui-button>
              </div>
            }
            <ui-button type="button" variant="secondary" (click)="r.answers.push({ text: '', points: 5, isActive: true })">
              + Respuesta
            </ui-button>
          </article>
        }
      </section>
    }

    @if (tab() === 'preview') {
      <section class="panel">
        <h2>Vista previa</h2>
        <p class="hint">Face-Off {{ draft.faceOffSeconds }}s · Control {{ draft.controlSeconds }}s · Robo {{ draft.stealSeconds }}s · X máx {{ draft.maxStrikes }}</p>
        @if (rounds()[previewIndex()]; as R) {
          <h3>{{ R.questionText || '(sin pregunta)' }}</h3>
          <ol>
            @for (a of R.answers; track $index) {
              @if (a.isActive !== false) {
                <li>{{ a.text || '—' }} · {{ a.points }} pts</li>
              }
            }
          </ol>
        } @else {
          <p>Selecciona una pregunta en la pestaña Preguntas.</p>
        }
      </section>
    }
  `,
  styles: [`
    .tabs { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; margin: 1rem 0; }
    .tab { border: 1px solid var(--color-border); background: var(--color-surface); border-radius: 999px; padding: 0.45rem 0.9rem; font-weight: 700; cursor: pointer; }
    .tab.on { background: color-mix(in srgb, var(--color-primary) 18%, transparent); border-color: var(--color-primary); color: var(--color-primary); }
    .back { margin-left: auto; font-weight: 700; color: var(--color-primary); }
    .panel { background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 16px; padding: 1rem; display: grid; gap: 0.85rem; }
    .grid2 { grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr)); }
    .field { display: grid; gap: 0.35rem; font-weight: 600; }
    .field.grow { flex: 1; }
    .input { min-height: 2.6rem; }
    .row { display: flex; flex-wrap: wrap; gap: 0.75rem; }
    .actions { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 0.75rem; }
    .check { display: flex; align-items: center; gap: 0.45rem; font-weight: 600; }
    .hint { color: var(--color-text-secondary); font-weight: 500; font-size: 0.85rem; }
    .dirty { font-weight: 700; color: var(--color-text-secondary); }
    .dirty.on { color: #d97706; }
    .round { border: 1px solid var(--color-border); border-radius: 12px; padding: 0.85rem; display: grid; gap: 0.55rem; }
    .round.off { opacity: 0.55; }
    .round header { display: flex; justify-content: space-between; gap: 0.5rem; flex-wrap: wrap; }
    .hist-actions { display: flex; flex-wrap: wrap; gap: 0.25rem; }
    .ans { display: grid; grid-template-columns: auto 1fr 5rem auto; gap: 0.4rem; align-items: center; }
    .pts { max-width: 5rem; }
  `]
})
export class GameShowAdminPage implements OnInit {
  private readonly api = inject(GameShowApi);

  readonly tabs: { id: AdminTab; label: string }[] = [
    { id: 'questions', label: 'Preguntas' },
    { id: 'times', label: 'Tiempos' },
    { id: 'rules', label: 'Reglas' },
    { id: 'preview', label: 'Vista previa' }
  ];

  readonly ruleFlags = [
    { key: 'enableFaceOff', label: 'Face-Off (guardado; motor actual siempre usa enfrentamiento)', disabled: false, hint: '' },
    { key: 'enableSteal', label: 'Robo tras X máximas', disabled: false, hint: '' },
    { key: 'enableLightning', label: 'Ronda relámpago (última)', disabled: false, hint: '' },
    { key: 'enableSounds', label: 'Sonidos', disabled: false, hint: '' },
    { key: 'enableAnimations', label: 'Animaciones / confeti', disabled: false, hint: '' },
    { key: 'allowPause', label: 'Permitir pausar', disabled: false, hint: '' },
    { key: 'allowSkipRound', label: 'Permitir saltar ronda', disabled: false, hint: '' },
    { key: 'allowHostEndRound', label: 'Permitir terminar ronda', disabled: false, hint: '' },
    { key: 'enableAudienceVote', label: 'Votación del público', disabled: true, hint: '(próximamente)' }
  ] as const;

  readonly tab = signal<AdminTab>('questions');
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly packs = signal<GameShowPackSummaryDto[]>([]);
  readonly selectedPackId = signal(0);
  readonly rounds = signal<GameShowRoundInput[]>([blankRound()]);
  readonly settingsDirty = signal(false);
  readonly savingSettings = signal(false);
  readonly savingPack = signal(false);
  readonly previewIndex = signal(0);

  draft: GameShowSettingsDto = emptySettings();
  private baseline: GameShowSettingsDto = emptySettings();
  packName = 'Pack de preguntas';
  query = '';
  filterActive: 'all' | 'on' | 'off' = 'all';

  readonly filteredRounds = computed(() => {
    const q = this.query.trim().toLowerCase();
    return this.rounds().filter((r) => {
      if (this.filterActive === 'on' && r.isActive === false) return false;
      if (this.filterActive === 'off' && r.isActive !== false) return false;
      if (!q) return true;
      const hay = `${r.questionText} ${r.category ?? ''}`.toLowerCase();
      return hay.includes(q);
    });
  });

  ngOnInit(): void {
    this.reloadSettings();
    this.api.listPacks().subscribe({
      next: (p) => this.packs.set(p),
      error: (e) => this.error.set(mapApiError(e))
    });
  }

  markDirty(): void {
    this.settingsDirty.set(true);
  }

  reloadSettings(): void {
    this.api.getGlobalSettings().subscribe({
      next: (s) => {
        this.draft = { ...s, enableAudienceVote: false };
        this.baseline = { ...this.draft };
        this.settingsDirty.set(false);
      },
      error: (e) => this.error.set(mapApiError(e))
    });
  }

  saveSettings(): void {
    this.savingSettings.set(true);
    this.error.set(null);
    this.api.putGlobalSettings({ ...this.draft, enableAudienceVote: false }).subscribe({
      next: (s) => {
        this.draft = { ...s, enableAudienceVote: false };
        this.baseline = { ...this.draft };
        this.settingsDirty.set(false);
        this.savingSettings.set(false);
        this.ok.set('Configuración global guardada');
      },
      error: (e) => {
        this.savingSettings.set(false);
        this.error.set(mapApiError(e));
      }
    });
  }

  restoreSettings(): void {
    if (!confirm('¿Restaurar tiempos y reglas a los valores predeterminados del juego?')) return;
    this.api.restoreGlobalSettings().subscribe({
      next: (s) => {
        this.draft = { ...s, enableAudienceVote: false };
        this.baseline = { ...this.draft };
        this.settingsDirty.set(false);
        this.ok.set('Valores predeterminados restaurados');
      },
      error: (e) => this.error.set(mapApiError(e))
    });
  }

  loadPack(id: number): void {
    this.selectedPackId.set(id);
    if (!id) {
      this.packName = 'Pack de preguntas';
      this.rounds.set([blankRound()]);
      return;
    }
    this.api.getPack(id).subscribe({
      next: (d) => {
        this.packName = d.name;
        this.rounds.set(
          d.body.rounds.map((r) => ({
            ...r,
            isActive: r.isActive !== false,
            category: r.category ?? '',
            answers: r.answers.map((a) => ({ ...a, isActive: a.isActive !== false }))
          }))
        );
      },
      error: (e) => this.error.set(mapApiError(e))
    });
  }

  addQuestion(): void {
    this.rounds.update((rs) => [...rs, blankRound()]);
  }

  realIndex(r: GameShowRoundInput): number {
    return this.rounds().indexOf(r);
  }

  removeRound(i: number): void {
    if (!confirm('¿Eliminar esta pregunta?')) return;
    this.rounds.update((rs) => rs.filter((_, idx) => idx !== i));
  }

  duplicateRound(i: number): void {
    const src = this.rounds()[i];
    if (!src) return;
    const copy: GameShowRoundInput = JSON.parse(JSON.stringify(src));
    copy.questionText = `${copy.questionText} (copia)`;
    this.rounds.update((rs) => {
      const next = [...rs];
      next.splice(i + 1, 0, copy);
      return next;
    });
  }

  moveRound(i: number, delta: number): void {
    const j = i + delta;
    this.rounds.update((rs) => {
      if (j < 0 || j >= rs.length) return rs;
      const next = [...rs];
      const tmp = next[i]!;
      next[i] = next[j]!;
      next[j] = tmp;
      return next;
    });
  }

  savePack(): void {
    const rounds = this.rounds();
    if (!rounds.some((r) => r.isActive !== false && r.questionText.trim().length >= 5)) {
      this.error.set('Necesitas al menos una pregunta activa válida.');
      return;
    }
    this.savingPack.set(true);
    const body = {
      name: this.packName.trim() || 'Pack de preguntas',
      notes: null as string | null,
      rounds
    };
    const id = this.selectedPackId();
    const req = id ? this.api.updatePack(id, body) : this.api.savePack(body);
    req.subscribe({
      next: (d) => {
        this.savingPack.set(false);
        this.selectedPackId.set(d.id);
        this.ok.set('Pack guardado');
        this.api.listPacks().subscribe({ next: (p) => this.packs.set(p) });
      },
      error: (e) => {
        this.savingPack.set(false);
        this.error.set(mapApiError(e));
      }
    });
  }
}
