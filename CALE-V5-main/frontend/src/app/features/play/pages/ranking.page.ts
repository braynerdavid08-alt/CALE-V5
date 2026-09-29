import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { PlayApi, Ranking, RankingScope } from '../api/play.api';
import { PlayTopbarComponent } from '../components/play-topbar.component';

@Component({
  selector: 'app-ranking-page',
  standalone: true,
  imports: [DatePipe, UiEmptyComponent, UiLoadingComponent, PlayTopbarComponent],
  styleUrl: './play-page.css',
  styles: [`
    .scopes { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .scope {
      padding: 0.45rem 0.95rem;
      border-radius: 999px;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
      font-weight: 600;
      cursor: pointer;
    }
    .scope.active { background: var(--color-primary); border-color: var(--color-primary); color: var(--color-on-primary); }
    .me { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 0.75rem; align-items: center; }
    .me strong { font-size: var(--text-lg); }
    .list { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.45rem; }
    .row {
      display: grid;
      grid-template-columns: 2.6rem 1fr auto;
      gap: 0.75rem;
      align-items: center;
      padding: 0.65rem 0.85rem;
      border-radius: var(--radius-md);
      background: var(--color-surface-raised);
    }
    .row.is-me { outline: 2px solid var(--color-primary); background: var(--color-primary-soft); }
    .pos { font-weight: 800; text-align: center; font-size: 1.1rem; }
    .xp { font-weight: 700; font-variant-numeric: tabular-nums; }
    .toggle { display: flex; gap: 0.6rem; align-items: flex-start; cursor: pointer; }
    .toggle input { width: 1.2rem; height: 1.2rem; margin-top: 0.15rem; accent-color: var(--color-primary); }
    .toggle small { display: block; color: var(--color-text-secondary); }
  `],
  template: `
    <section class="play-page">
      <play-topbar
        title="Ranking semanal"
        subtitle="Suma experiencia practicando. El ranking se reinicia cada lunes." />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (data(); as d) {
        <div class="play-card">
          <label class="toggle">
            <input type="checkbox" [checked]="d.showInRanking" [disabled]="saving()" (change)="toggleVisibility($event)" />
            <span>
              <strong>Aparecer en el ranking</strong>
              <small>Solo se muestra tu nombre y la inicial de tu apellido. Puedes ocultarte cuando quieras.</small>
            </span>
          </label>
        </div>

        @if (d.scopes.length > 1) {
          <div class="scopes" role="tablist" aria-label="Tipo de ranking">
            @for (s of d.scopes; track s.scope + (s.groupId ?? '')) {
              <button
                type="button"
                role="tab"
                class="scope"
                [class.active]="isActive(s, d)"
                [attr.aria-selected]="isActive(s, d)"
                (click)="select(s)">
                {{ s.label }}
              </button>
            }
          </div>
        }

        <div class="play-card me">
          <span>
            <small class="muted">Semana del {{ d.weekStart | date: 'd MMM' }} al {{ d.weekEnd | date: 'd MMM' }} · {{ d.scopeLabel }}</small><br />
            <strong>
              @if (d.myPosition) { Vas en el puesto #{{ d.myPosition }} de {{ d.participants }} }
              @else if (!d.showInRanking) { Estás oculto del ranking }
              @else { Aún no sumas puntos esta semana }
            </strong>
          </span>
          <span class="chip good">⭐ {{ d.myXp }} XP esta semana</span>
        </div>

        @if (loading()) {
          <ui-loading />
        } @else if (d.entries.length) {
          <ol class="list">
            @for (e of d.entries; track e.userId) {
              <li class="row" [class.is-me]="e.isMe">
                <span class="pos">{{ medal(e.position) }}</span>
                <span>{{ e.displayName }}{{ e.isMe ? ' (tú)' : '' }}</span>
                <span class="xp">{{ e.xp }} XP</span>
              </li>
            }
          </ol>
        } @else {
          <ui-empty title="Nadie ha sumado puntos esta semana" message="¡Haz el reto diario o un simulacro y sé el primero!" />
        }
      } @else if (loading()) {
        <ui-loading />
      }
    </section>
  `
})
export class RankingPage implements OnInit {
  private readonly api = inject(PlayApi);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly data = signal<Ranking | null>(null);
  private scope: string | null = null;
  private groupId: number | null = null;

  ngOnInit(): void {
    this.load();
  }

  select(s: RankingScope): void {
    this.scope = s.scope;
    this.groupId = s.groupId ?? null;
    this.load();
  }

  isActive(s: RankingScope, d: Ranking): boolean {
    return s.scope === d.scope && (s.scope !== 'group' || s.groupId === d.groupId);
  }

  toggleVisibility(event: Event): void {
    const show = (event.target as HTMLInputElement).checked;
    this.saving.set(true);
    this.api.setRankingVisibility(show).subscribe({
      next: () => {
        this.saving.set(false);
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  medal(position: number): string {
    return position === 1 ? '🥇' : position === 2 ? '🥈' : position === 3 ? '🥉' : `${position}`;
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.ranking(this.scope, this.groupId).subscribe({
      next: (d) => {
        this.data.set(d);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }
}
