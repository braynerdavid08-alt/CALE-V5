import { Component, OnInit, inject, signal } from '@angular/core';
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

function emptyRound(): GameShowRoundInput {
  return {
    questionText: '',
    answers: [
      { text: '', points: 30 },
      { text: '', points: 25 },
      { text: '', points: 20 },
      { text: '', points: 15 },
      { text: '', points: 10 }
    ]
  };
}

@Component({
  selector: 'app-game-show-hub-page',
  standalone: true,
  imports: [FormsModule, RouterLink, UiButtonComponent, UiErrorComponent, UiPageHeaderComponent],
  template: `
    <ui-page-header
      title="100 Estudiantes Dijeron"
      subtitle="Juego de clase con dos equipos, TOP 5 y buzzer en tiempo real." />
    <ui-error [message]="error()" />

    <section class="panel">
      <h2>Crear partida</h2>
      <label class="field">Nombre
        <input class="input" [(ngModel)]="title" name="title" />
      </label>
      <div class="row">
        <label class="field">Equipo A
          <input class="input" [(ngModel)]="teamA" name="teamA" />
        </label>
        <label class="field">Equipo B
          <input class="input" [(ngModel)]="teamB" name="teamB" />
        </label>
      </div>

      @for (round of rounds; track $index; let ri = $index) {
        <article class="round">
          <header>
            <strong>Ronda {{ ri + 1 }}</strong>
            @if (rounds.length > 1) {
              <ui-button type="button" variant="ghost" (click)="removeRound(ri)">Quitar</ui-button>
            }
          </header>
          <label class="field">Pregunta
            <textarea class="input" rows="2" [(ngModel)]="round.questionText" [name]="'q'+ri"></textarea>
          </label>
          <div class="answers">
            @for (ans of round.answers; track $index; let ai = $index) {
              <div class="ans-row">
                <span>#{{ ai + 1 }}</span>
                <input class="input" placeholder="Respuesta" [(ngModel)]="ans.text" [name]="'a'+ri+'-'+ai" />
                <input class="input pts" type="number" min="1" [(ngModel)]="ans.points" [name]="'p'+ri+'-'+ai" />
              </div>
            }
          </div>
        </article>
      }

      <div class="actions">
        <ui-button type="button" variant="secondary" (click)="addRound()">+ Ronda</ui-button>
        <ui-button type="button" variant="secondary" (click)="exportDraft('csv')">Exportar borrador CSV</ui-button>
        <ui-button type="button" variant="secondary" (click)="exportDraft('json')">Exportar borrador JSON</ui-button>
        <ui-button type="button" [loading]="saving()" (click)="create()">Crear partida</ui-button>
      </div>
    </section>

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
    .panel { background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 16px; padding: 1.25rem; margin-bottom: 1rem; display: grid; gap: 0.85rem; }
    .row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .field { display: grid; gap: 0.35rem; font-weight: 600; }
    .input { width: 100%; }
    .round { border: 1px solid var(--color-border); border-radius: 12px; padding: 0.85rem; display: grid; gap: 0.65rem; }
    .round header { display: flex; justify-content: space-between; align-items: center; }
    .answers { display: grid; gap: 0.45rem; }
    .ans-row { display: grid; grid-template-columns: 2rem 1fr 5rem; gap: 0.45rem; align-items: center; }
    .pts { max-width: 5rem; }
    .actions { display: flex; flex-wrap: wrap; gap: 0.65rem; }
    .hist { list-style: none; padding: 0; margin: 0; display: grid; gap: 0.55rem; }
    .hist li { display: flex; justify-content: space-between; gap: 1rem; flex-wrap: wrap; align-items: center; }
    .hist-main { display: grid; gap: 0.2rem; min-width: 0; }
    .hist-actions { display: flex; gap: 0.35rem; flex-shrink: 0; }
    @media (max-width: 700px) { .row, .ans-row { grid-template-columns: 1fr; } }
  `]
})
export class GameShowHubPage implements OnInit {
  private readonly api = inject(GameShowApi);
  private readonly router = inject(Router);

  readonly error = signal<string | null>(null);
  readonly saving = signal(false);
  readonly history = signal<GameShowHistoryItemDto[]>([]);

  title = '100 Estudiantes Dijeron';
  teamA = 'Equipo A';
  teamB = 'Equipo B';
  rounds: GameShowRoundInput[] = [emptyRound()];

  ngOnInit(): void {
    this.api.mine().subscribe({
      next: (items) => this.history.set(items),
      error: () => this.history.set([])
    });
  }

  addRound(): void {
    this.rounds = [...this.rounds, emptyRound()];
  }

  removeRound(index: number): void {
    this.rounds = this.rounds.filter((_, i) => i !== index);
  }

  exportDraft(format: 'csv' | 'json'): void {
    const body: CreateGameShowBody = {
      title: this.title,
      teamAName: this.teamA,
      teamBName: this.teamB,
      rounds: this.rounds
    };
    if (format === 'json') {
      const blob = new Blob([JSON.stringify(body, null, 2)], { type: 'application/json;charset=utf-8' });
      this.saveBlob(blob, `cale-100-dijeron-borrador.json`);
      return;
    }
    const lines = ['ronda,pregunta,rank,respuesta,puntos,aliases'];
    this.rounds.forEach((round, ri) => {
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
    this.error.set(null);
    this.saving.set(true);
    const body: CreateGameShowBody = {
      title: this.title,
      teamAName: this.teamA,
      teamBName: this.teamB,
      rounds: this.rounds
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

function csv(value: string): string {
  return `"${(value || '').replace(/"/g, '""')}"`;
}
