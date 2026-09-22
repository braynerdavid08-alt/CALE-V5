import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  CreateGameShowBody,
  GameShowApi,
  GameShowPackSummaryDto
} from '../api/game-show.api';
import { GameShowDraftStore } from '../api/game-show-draft.store';

type PackChoice = 'official' | `pack:${number}` | 'draft';

@Component({
  selector: 'app-game-show-hub-page',
  standalone: true,
  imports: [FormsModule, UiButtonComponent, UiErrorComponent, UiPageHeaderComponent],
  template: `
    <ui-page-header
      title="100 Estudiantes Dijeron"
      subtitle="Elige un banco de preguntas, arma la sala y juega." />
    <div class="admin-bar">
      <ui-button type="button" variant="secondary" routerLink="/teacher/game-show/admin">
        Panel de configuración
      </ui-button>
      <span class="admin-hint">Preguntas, importar/exportar, tiempos y reglas</span>
    </div>
    <ui-error [message]="error()" />

    <section class="panel">
      <header class="panel-head">
        <div>
          <h2>Crear sala</h2>
          <p class="sub">Escoge el banco de preguntas y define los equipos.</p>
        </div>
      </header>

      <label class="field">Banco de preguntas
        <select
          class="input"
          [ngModel]="packChoice()"
          (ngModelChange)="onPackChoice($event)"
          name="packChoice">
          <option value="official">Pack oficial (41 rondas)</option>
          @for (p of packs(); track p.id) {
            <option [value]="'pack:' + p.id">{{ p.name }} ({{ p.roundCount }} rondas)</option>
          }
          @if (readyCount() > 0 && draftStore.draft().packSource === 'draft') {
            <option value="draft">Borrador local ({{ readyCount() }} válidas)</option>
          }
        </select>
      </label>

      @if (loadingChoice()) {
        <p class="hint">Cargando banco…</p>
      } @else {
        <div class="pack-summary">
          <div>
            <span class="label">Banco seleccionado</span>
            <strong>{{ selectedLabel() }}</strong>
          </div>
          <p class="hint">
            {{ readyCount() }} rondas válidas listas para jugar.
            @if (readyCount() < 1) {
              Abre el
              <ui-button type="button" variant="ghost" routerLink="/teacher/game-show/admin">
                Panel de configuración
              </ui-button>
              para crear, importar o editar packs.
            }
          </p>
        </div>
      }

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

      <div class="actions">
        <ui-button
          type="button"
          [loading]="saving()"
          [disabled]="readyCount() < 1 || loadingChoice()"
          (click)="create()">
          Crear sala e ir al control
        </ui-button>
      </div>
    </section>
  `,
  styles: [`
    .admin-bar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
      margin: 0 0 1rem;
    }
    .admin-hint { color: var(--color-text-secondary); font-size: 0.9rem; }
    .panel {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 16px;
      padding: 1.25rem;
      margin-bottom: 1rem;
      display: grid;
      gap: 0.85rem;
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
    .label { display: block; font-size: 0.8rem; color: var(--color-text-secondary); font-weight: 700; }
    .hint { margin: 0; color: var(--color-text-secondary); }
    .actions { display: flex; flex-wrap: wrap; gap: 0.55rem; }
    @media (max-width: 720px) {
      .row { grid-template-columns: 1fr; }
    }
  `]
})
export class GameShowHubPage implements OnInit {
  private readonly api = inject(GameShowApi);
  private readonly router = inject(Router);
  readonly draftStore = inject(GameShowDraftStore);

  readonly error = signal<string | null>(null);
  readonly saving = signal(false);
  readonly loadingChoice = signal(false);
  readonly packs = signal<GameShowPackSummaryDto[]>([]);
  readonly packChoice = signal<PackChoice>('official');

  readonly title = computed(() => this.draftStore.draft().title);
  readonly teamA = computed(() => this.draftStore.draft().teamAName);
  readonly teamB = computed(() => this.draftStore.draft().teamBName);
  readonly readyCount = computed(() => this.draftStore.readyRoundCount());

  readonly selectedLabel = computed(() => {
    const choice = this.packChoice();
    if (choice === 'official') return 'Pack oficial';
    if (choice === 'draft') return 'Borrador local';
    const id = Number(choice.slice(5));
    const pack = this.packs().find((p) => p.id === id);
    return pack?.name || `Pack #${id}`;
  });

  ngOnInit(): void {
    this.api.listPacks().subscribe({
      next: (items) => this.packs.set(items),
      error: () => this.packs.set([])
    });

    if (this.readyCount() < 1) {
      this.onPackChoice('official');
    } else if (this.draftStore.draft().packSource === 'pack') {
      const packId = this.draftStore.draft().selectedPackId;
      if (packId != null) {
        this.packChoice.set(`pack:${packId}`);
      } else {
        this.packChoice.set('draft');
      }
    } else if (this.draftStore.draft().packSource === 'official') {
      this.packChoice.set('official');
    } else {
      this.packChoice.set('draft');
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

  onPackChoice(value: string): void {
    const choice = value as PackChoice;
    this.packChoice.set(choice);
    this.error.set(null);

    if (choice === 'draft') {
      this.draftStore.selectPack(null, 'draft');
      return;
    }

    if (choice === 'official') {
      this.loadingChoice.set(true);
      this.api.officialPack().subscribe({
        next: (body) => {
          this.draftStore.applyPack(body, { packId: null, source: 'official' });
          this.loadingChoice.set(false);
        },
        error: (err) => {
          this.loadingChoice.set(false);
          this.error.set(mapApiError(err));
        }
      });
      return;
    }

    const packId = Number(choice.slice(5));
    if (!Number.isFinite(packId)) return;
    this.loadingChoice.set(true);
    this.api.getPack(packId).subscribe({
      next: (detail) => {
        this.draftStore.applyPack(detail.body, { packId: detail.id, source: 'pack' });
        this.loadingChoice.set(false);
      },
      error: (err) => {
        this.loadingChoice.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  create(): void {
    if (this.readyCount() < 1) {
      this.error.set('Elige un banco con al menos una ronda válida.');
      return;
    }

    this.error.set(null);
    this.saving.set(true);

    const choice = this.packChoice();
    const room = {
      title: this.title(),
      teamAName: this.teamA(),
      teamBName: this.teamB()
    };

    if (choice.startsWith('pack:')) {
      const packId = Number(choice.slice(5));
      this.api.createFromPack(packId, room).subscribe({
        next: (lobby) => {
          this.saving.set(false);
          void this.router.navigate(['/teacher/game-show', lobby.id, 'host']);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(mapApiError(err));
        }
      });
      return;
    }

    const full = this.draftStore.toCreateBody();
    const body: CreateGameShowBody = {
      ...full,
      ...room,
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
}
