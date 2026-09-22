import { Component, ElementRef, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  CreateGameShowBody,
  GameShowApi,
  GameShowHistoryItemDto,
  GameShowRoundInput
} from '../api/game-show.api';
import { GameShowDraftStore } from '../api/game-show-draft.store';
import { GameShowImportError, parseGameShowImport } from '../api/game-show-import';

@Component({
  selector: 'app-game-show-hub-page',
  standalone: true,
  imports: [FormsModule, RouterLink, UiButtonComponent, UiErrorComponent, UiPageHeaderComponent],
  template: `
    <ui-page-header
      title="100 Estudiantes Dijeron"
      subtitle="Prepara el pack de preguntas y, aparte, crea la sala para jugar en vivo." />
    <ui-error [message]="error()" />

    <div class="tabs" role="tablist" aria-label="Secciones del juego">
      <button
        type="button"
        class="tab"
        role="tab"
        [class.on]="tab() === 'room'"
        [attr.aria-selected]="tab() === 'room'"
        (click)="tab.set('room')">
        Crear sala
      </button>
      <button
        type="button"
        class="tab"
        role="tab"
        [class.on]="tab() === 'pack'"
        [attr.aria-selected]="tab() === 'pack'"
        (click)="tab.set('pack')">
        Preguntas / pack
        <span class="count">{{ roundCount() }}</span>
      </button>
    </div>

    @if (tab() === 'room') {
      <section class="panel" role="tabpanel">
        <header class="panel-head">
          <div>
            <h2>Crear sala</h2>
            <p class="sub">Define el nombre y los equipos. Las preguntas se toman del pack listo.</p>
          </div>
          <ui-button type="button" variant="ghost" (click)="tab.set('pack')">Editar preguntas</ui-button>
        </header>

        <label class="field">Nombre de la partida
          <input class="input" [ngModel]="title()" (ngModelChange)="onTitle($event)" name="title" />
        </label>
        <div class="row">
          <label class="field">Equipo A
            <input class="input" [ngModel]="teamA()" (ngModelChange)="onTeamA($event)" name="teamA" />
          </label>
          <label class="field">Equipo B
            <input class="input" [ngModel]="teamB()" (ngModelChange)="onTeamB($event)" name="teamB" />
          </label>
        </div>

        <div class="pack-summary">
          <div>
            <span class="label">Pack listo</span>
            <strong>{{ readyCount() }} / {{ roundCount() }} rondas válidas</strong>
          </div>
          <p class="hint">
            @if (readyCount() < 1) {
              Aún no hay rondas válidas. Ve a <button type="button" class="linkish" (click)="tab.set('pack')">Preguntas / pack</button>
              para cargar el pack oficial o editar rondas.
            } @else {
              Al crear la sala se usarán las {{ readyCount() }} rondas válidas del pack.
            }
          </p>
        </div>

        <div class="actions">
          <ui-button
            type="button"
            [loading]="saving()"
            [disabled]="readyCount() < 1"
            (click)="create()">
            Crear sala e ir al control
          </ui-button>
          <ui-button type="button" variant="secondary" (click)="tab.set('pack')">
            Ir a preguntas
          </ui-button>
        </div>
      </section>
    } @else {
      <section class="panel" role="tabpanel">
        <header class="panel-head">
          <div>
            <h2>Preguntas / pack</h2>
            <p class="sub">Edita, importa o carga el pack oficial. Queda guardado como borrador para crear la sala.</p>
          </div>
          <ui-button type="button" variant="ghost" (click)="tab.set('room')">Crear sala</ui-button>
        </header>

        <div class="actions">
          <ui-button type="button" variant="secondary" (click)="addRound()">+ Ronda</ui-button>
          <ui-button type="button" variant="secondary" (click)="exportDraft('csv')">Exportar CSV</ui-button>
          <ui-button type="button" variant="secondary" (click)="exportDraft('json')">Exportar JSON</ui-button>
          <ui-button type="button" variant="secondary" (click)="pickImport()" [loading]="importing()">
            Importar JSON/CSV
          </ui-button>
          <ui-button type="button" variant="secondary" (click)="loadOfficialPack()" [loading]="loadingPack()">
            Cargar pack oficial (41)
          </ui-button>
          <input
            #importInput
            class="file-input"
            type="file"
            accept=".json,.csv,application/json,text/csv,text/plain"
            (change)="onImportFile($event)" />
        </div>
        @if (loadedInfo()) {
          <p class="loaded">{{ loadedInfo() }}</p>
        }
        <p class="hint">Borrador guardado en este navegador. Cuando esté listo, vuelve a <strong>Crear sala</strong>.</p>

        @for (round of rounds(); track $index; let ri = $index) {
          <article class="round">
            <header>
              <strong>Ronda {{ ri + 1 }}</strong>
              @if (rounds().length > 1) {
                <ui-button type="button" variant="ghost" (click)="removeRound(ri)">Quitar</ui-button>
              }
            </header>
            <label class="field">Pregunta
              <textarea
                class="input"
                rows="2"
                [ngModel]="round.questionText"
                (ngModelChange)="onQuestion(ri, $event)"
                [name]="'q'+ri"></textarea>
            </label>
            <div class="answers">
              @for (ans of round.answers; track $index; let ai = $index) {
                <div class="ans-row">
                  <span>#{{ ai + 1 }}</span>
                  <input
                    class="input"
                    placeholder="Respuesta"
                    [ngModel]="ans.text"
                    (ngModelChange)="onAnswerText(ri, ai, $event)"
                    [name]="'a'+ri+'-'+ai" />
                  <input
                    class="input pts"
                    type="number"
                    min="1"
                    [ngModel]="ans.points"
                    (ngModelChange)="onAnswerPoints(ri, ai, $event)"
                    [name]="'p'+ri+'-'+ai" />
                  <input
                    class="input"
                    placeholder="Aliases (a | b)"
                    [ngModel]="aliasesText(ans.aliases)"
                    (ngModelChange)="onAnswerAliases(ri, ai, $event)"
                    [name]="'al'+ri+'-'+ai" />
                </div>
              }
            </div>
          </article>
        }

        <div class="actions">
          <ui-button type="button" (click)="tab.set('room')">Listo · ir a crear sala</ui-button>
        </div>
      </section>
    }

    @if (history().length) {
      <section class="panel">
        <h2>Partidas recientes</h2>
        <ul class="hist">
          @for (h of history(); track h.id) {
            <li>
              <div class="hist-main">
                <a [routerLink]="['/teacher/game-show', h.id, 'host']">{{ h.title }}</a>
                <span>{{ h.teamAScore }} – {{ h.teamBScore }} · {{ h.status }}</span>
              </div>
              <div class="hist-actions">
                <ui-button type="button" variant="ghost" (click)="exportSession(h.id, 'csv')">CSV</ui-button>
                <ui-button type="button" variant="ghost" (click)="exportSession(h.id, 'json')">JSON</ui-button>
              </div>
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: [`
    .tabs {
      display: flex;
      gap: 0.35rem;
      margin: 0 0 1rem;
      padding: 0.3rem;
      border-radius: 14px;
      background: color-mix(in srgb, var(--color-surface) 80%, transparent);
      border: 1px solid var(--color-border);
      width: fit-content;
      max-width: 100%;
      flex-wrap: wrap;
    }
    .tab {
      border: 0;
      background: transparent;
      color: var(--color-text-secondary);
      font: inherit;
      font-weight: 700;
      padding: 0.65rem 1rem;
      border-radius: 10px;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 0.45rem;
    }
    .tab.on {
      background: var(--color-primary);
      color: var(--color-on-primary, #fff);
    }
    .count {
      font-size: 0.75rem;
      font-weight: 800;
      padding: 0.1rem 0.45rem;
      border-radius: 999px;
      background: color-mix(in srgb, currentColor 16%, transparent);
    }
    .panel {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 16px;
      padding: 1.25rem;
      margin-bottom: 1rem;
      display: grid;
      gap: 0.85rem;
    }
    .panel-head {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: flex-start;
      flex-wrap: wrap;
    }
    .panel-head h2 { margin: 0; }
    .sub { margin: 0.35rem 0 0; color: var(--color-text-secondary); }
    .row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .field { display: grid; gap: 0.35rem; font-weight: 600; }
    .input { width: 100%; }
    .pack-summary {
      border: 1px solid var(--color-border);
      border-radius: 12px;
      padding: 0.9rem 1rem;
      background: color-mix(in srgb, var(--color-primary) 6%, transparent);
      display: grid;
      gap: 0.35rem;
    }
    .label {
      display: block;
      font-size: 0.75rem;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      color: var(--color-text-secondary);
      margin-bottom: 0.2rem;
    }
    .round { border: 1px solid var(--color-border); border-radius: 12px; padding: 0.85rem; display: grid; gap: 0.65rem; }
    .round header { display: flex; justify-content: space-between; align-items: center; }
    .answers { display: grid; gap: 0.45rem; }
    .ans-row { display: grid; grid-template-columns: 2rem 1fr 5rem 1fr; gap: 0.45rem; align-items: center; }
    .pts { max-width: 5rem; }
    .actions { display: flex; flex-wrap: wrap; gap: 0.65rem; align-items: center; }
    .file-input { position: absolute; width: 1px; height: 1px; opacity: 0; overflow: hidden; }
    .hint { margin: 0; color: var(--color-muted, #6b7280); font-size: 0.9rem; }
    .loaded { margin: 0; color: var(--color-success); font-weight: 700; }
    .linkish {
      border: 0;
      background: none;
      color: var(--color-primary);
      font: inherit;
      font-weight: 700;
      cursor: pointer;
      padding: 0;
      text-decoration: underline;
    }
    .hist { list-style: none; padding: 0; margin: 0; display: grid; gap: 0.55rem; }
    .hist li { display: flex; justify-content: space-between; gap: 1rem; flex-wrap: wrap; align-items: center; }
    .hist-main { display: grid; gap: 0.2rem; min-width: 0; }
    .hist-actions { display: flex; gap: 0.35rem; flex-shrink: 0; }
    @media (max-width: 700px) {
      .row { grid-template-columns: 1fr; }
      .ans-row { grid-template-columns: 2rem 1fr; }
      .pts { max-width: none; }
    }
  `]
})
export class GameShowHubPage implements OnInit {
  private readonly api = inject(GameShowApi);
  private readonly router = inject(Router);
  private readonly draftStore = inject(GameShowDraftStore);

  readonly error = signal<string | null>(null);
  readonly saving = signal(false);
  readonly importing = signal(false);
  readonly loadingPack = signal(false);
  readonly loadedInfo = signal<string | null>(null);
  readonly history = signal<GameShowHistoryItemDto[]>([]);
  readonly tab = signal<'room' | 'pack'>('room');

  readonly title = computed(() => this.draftStore.draft().title);
  readonly teamA = computed(() => this.draftStore.draft().teamAName);
  readonly teamB = computed(() => this.draftStore.draft().teamBName);
  readonly rounds = computed(() => this.draftStore.draft().rounds);
  readonly roundCount = computed(() => this.rounds().length);
  readonly readyCount = computed(() => this.draftStore.readyRoundCount());

  @ViewChild('importInput') private importInput?: ElementRef<HTMLInputElement>;

  ngOnInit(): void {
    this.api.mine().subscribe({
      next: (items) => this.history.set(items),
      error: () => this.history.set([])
    });
    if (this.readyCount() < 1) {
      this.tab.set('pack');
    }
  }

  onTitle(value: string): void {
    this.draftStore.setRoom(value, this.teamA(), this.teamB());
  }

  onTeamA(value: string): void {
    this.draftStore.setRoom(this.title(), value, this.teamB());
  }

  onTeamB(value: string): void {
    this.draftStore.setRoom(this.title(), this.teamA(), value);
  }

  addRound(): void {
    this.draftStore.setRounds([...this.rounds(), this.draftStore.emptyRound()]);
  }

  removeRound(index: number): void {
    this.draftStore.setRounds(this.rounds().filter((_, i) => i !== index));
  }

  onQuestion(ri: number, value: string): void {
    const next = this.cloneRounds();
    next[ri] = { ...next[ri], questionText: value };
    this.draftStore.setRounds(next);
  }

  onAnswerText(ri: number, ai: number, value: string): void {
    const next = this.cloneRounds();
    const answers = [...next[ri].answers];
    answers[ai] = { ...answers[ai], text: value };
    next[ri] = { ...next[ri], answers };
    this.draftStore.setRounds(next);
  }

  onAnswerPoints(ri: number, ai: number, value: number): void {
    const next = this.cloneRounds();
    const answers = [...next[ri].answers];
    answers[ai] = { ...answers[ai], points: Number(value) || 1 };
    next[ri] = { ...next[ri], answers };
    this.draftStore.setRounds(next);
  }

  onAnswerAliases(ri: number, ai: number, value: string): void {
    const next = this.cloneRounds();
    const answers = [...next[ri].answers];
    answers[ai] = {
      ...answers[ai],
      aliases: (value || '')
        .split('|')
        .map((x) => x.trim())
        .filter(Boolean)
    };
    next[ri] = { ...next[ri], answers };
    this.draftStore.setRounds(next);
  }

  aliasesText(aliases?: string[]): string {
    return (aliases || []).join(' | ');
  }

  pickImport(): void {
    this.importInput?.nativeElement.click();
  }

  loadOfficialPack(): void {
    this.error.set(null);
    this.loadingPack.set(true);
    this.api.officialPack().subscribe({
      next: (body) => {
        this.applyImport(body);
        this.loadingPack.set(false);
      },
      error: (err) => {
        this.loadingPack.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  onImportFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.error.set(null);
    this.importing.set(true);

    this.api.importQuestions(file).subscribe({
      next: (body) => {
        this.applyImport(body);
        this.importing.set(false);
      },
      error: (err) => {
        const reader = new FileReader();
        reader.onload = () => {
          try {
            const body = parseGameShowImport(String(reader.result ?? ''), file.name);
            this.applyImport(body);
            this.importing.set(false);
          } catch (localErr) {
            this.importing.set(false);
            this.error.set(
              localErr instanceof GameShowImportError ? localErr.message : mapApiError(err)
            );
          }
        };
        reader.onerror = () => {
          this.importing.set(false);
          this.error.set(mapApiError(err));
        };
        reader.readAsText(file, 'utf-8');
      }
    });
  }

  private applyImport(body: CreateGameShowBody): void {
    this.draftStore.applyPack(body);
    this.loadedInfo.set(`Rondas cargadas: ${this.roundCount()}`);
  }

  exportDraft(format: 'csv' | 'json'): void {
    const body = this.draftStore.toCreateBody();
    if (format === 'json') {
      const blob = new Blob([JSON.stringify(body, null, 2)], { type: 'application/json;charset=utf-8' });
      this.saveBlob(blob, `cale-100-dijeron-borrador.json`);
      return;
    }
    const lines = ['ronda,pregunta,rank,respuesta,puntos,aliases'];
    body.rounds.forEach((round, ri) => {
      round.answers.forEach((ans, ai) => {
        lines.push(
          `${ri + 1},${csv(round.questionText)},${ai + 1},${csv(ans.text)},${ans.points},${csv((ans.aliases || []).join(' | '))}`
        );
      });
    });
    const blob = new Blob(['\uFEFF' + lines.join('\n')], { type: 'text/csv;charset=utf-8' });
    this.saveBlob(blob, `cale-100-dijeron-borrador.csv`);
  }

  exportSession(id: number, format: 'csv' | 'json'): void {
    this.error.set(null);
    this.api.exportQuestions(id, format).subscribe({
      next: (blob) => this.saveBlob(blob, `cale-100-dijeron-${id}.${format}`),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private saveBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  create(): void {
    if (this.readyCount() < 1) {
      this.error.set('Prepara al menos una ronda válida en Preguntas / pack.');
      this.tab.set('pack');
      return;
    }

    this.error.set(null);
    this.saving.set(true);
    const full = this.draftStore.toCreateBody();
    // Only send rounds that pass basic validity so create doesn't fail mid-pack.
    const body: CreateGameShowBody = {
      ...full,
      rounds: full.rounds.filter((r) =>
        (r.questionText || '').trim().length >= 5
        && (r.answers || []).filter((a) => (a.text || '').trim().length > 0).length === 5
      )
    };
    this.api.create(body).subscribe({
      next: (lobby) => {
        this.saving.set(false);
        void this.router.navigate(['/teacher/game-show', lobby.id, 'host']);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private cloneRounds(): GameShowRoundInput[] {
    return this.rounds().map((r) => ({
      ...r,
      answers: r.answers.map((a) => ({ ...a, aliases: [...(a.aliases || [])] }))
    }));
  }
}

function csv(value: string): string {
  return `"${(value || '').replace(/"/g, '""')}"`;
}
