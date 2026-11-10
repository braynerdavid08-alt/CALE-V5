import { Component, OnInit, inject, signal } from '@angular/core';
import { mapApiError } from '../../../core/http/map-api-error';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { PlayApi, Ranking, RankingEntry, RankingScope } from '../api/play.api';
import { PlayTopbarComponent } from '../components/play-topbar.component';
import { rankingHeldLabel } from '../ranking-tenure';

@Component({
  selector: 'app-ranking-page',
  standalone: true,
  imports: [UiEmptyComponent, UiLoadingComponent, PlayTopbarComponent],
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
    .metrics { margin-top: 0.75rem; }
    .me { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 0.75rem; align-items: center; }
    .me strong { font-size: var(--text-lg); }
    .list { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.45rem; }
    .row {
      display: grid;
      grid-template-columns: 2.6rem 2.6rem 1fr auto;
      gap: 0.65rem;
      align-items: center;
      padding: 0.65rem 0.85rem;
      border-radius: var(--radius-md);
      background: var(--color-surface-raised);
    }
    .row.is-me { outline: 2px solid var(--color-primary); background: var(--color-primary-soft); }
    .pos { font-weight: 800; text-align: center; font-size: 1.1rem; }
    .avatar {
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 999px;
      overflow: hidden;
      background: var(--color-chip, #e2e8f0);
      display: grid;
      place-items: center;
      font-weight: 800;
      font-size: 0.85rem;
      color: var(--color-text-secondary);
      flex-shrink: 0;
    }
    .avatar img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .who { min-width: 0; display: grid; gap: 0.1rem; }
    .who strong { font-weight: 700; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .held { font-size: 0.78rem; color: var(--color-text-secondary); }
    .xp { font-weight: 700; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .toggle { display: flex; gap: 0.6rem; align-items: flex-start; cursor: pointer; }
    .toggle input { width: 1.2rem; height: 1.2rem; margin-top: 0.15rem; accent-color: var(--color-primary); }
    .toggle small { display: block; color: var(--color-text-secondary); }
  `],
  template: `
    <section class="play-page">
      <play-topbar
        title="Ranking"
        subtitle="XP y trofeos · Histórico general · Mi escuela, Mi grupo o Todo Luz Verde." />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (data(); as d) {
        <div class="play-card">
          <label class="toggle">
            <input type="checkbox" [checked]="d.showInRanking" [disabled]="saving()" (change)="toggleVisibility($event)" />
            <span>
              <strong>Aparecer en el ranking</strong>
              <small>Se muestra tu nombre (con inicial del apellido) y tu foto si la subiste. Puedes ocultarte cuando quieras.</small>
            </span>
          </label>
        </div>

        <div class="scopes metrics" role="tablist" aria-label="Clasificación">
          @for (m of metrics; track m.key) {
            <button type="button" role="tab" class="scope" [class.active]="metric === m.key" (click)="selectMetric(m.key)">
              {{ m.label }}
            </button>
          }
        </div>

        @if (d.scopes.length > 1) {
          <div class="scopes" role="tablist" aria-label="Ámbito">
            @for (s of d.scopes; track $index) {
              <button
                type="button"
                role="tab"
                class="scope"
                [class.active]="isActive(s)"
                [attr.aria-selected]="isActive(s)"
                (click)="select(s)">
                {{ s.label }}
              </button>
            }
          </div>
        }

        <div class="play-card me">
          <span>
            <small class="muted">Histórico general · {{ d.scopeLabel }}</small><br />
            <strong>
              @if (d.myPosition) { Vas en el puesto #{{ d.myPosition }} de {{ d.participants }} }
              @else if (!d.showInRanking) { Estás oculto del ranking }
              @else { Aún no sumas puntos en este ranking }
            </strong>
          </span>
          <span class="chip good">
            {{ d.myXp }} XP · {{ d.myTrophies ?? 0 }} 🏆
          </span>
        </div>

        @if (loading()) {
          <ui-loading />
        } @else if (d.entries.length) {
          <ol class="list">
            @for (e of d.entries; track e.userId) {
              <li class="row" [class.is-me]="e.isMe">
                <span class="pos">{{ medal(e.position) }}</span>
                <span class="avatar" aria-hidden="true">
                  @if (photoSrc(e); as src) {
                    <img [src]="src" [alt]="''" />
                  } @else {
                    {{ initials(e.displayName) }}
                  }
                </span>
                <span class="who">
                  <strong>{{ e.displayName }}{{ e.isMe ? ' (tú)' : '' }}</strong>
                  @if (heldLabel(e); as held) {
                    <span class="held">{{ held }}</span>
                  }
                </span>
                <span class="xp">{{ scoreLabel(e) }}</span>
              </li>
            }
          </ol>
        } @else {
          <ui-empty
            title="Nadie ha sumado puntos aún"
            message="¡Haz el reto diario, un simulacro o un torneo y sé el primero del ranking!" />
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
  readonly metrics = [
    { key: 'xp', label: 'XP' },
    { key: 'trophies', label: 'Trofeos' }
  ];
  private scope: string | null = null;
  /** Always historical cumulative; date filters removed. */
  private readonly period = 'all';
  metric = 'xp';

  ngOnInit(): void {
    this.load();
  }

  select(s: RankingScope): void {
    this.scope = s.scope;
    this.load();
  }

  selectMetric(key: string): void {
    this.metric = key;
    this.load();
  }

  isActive(s: RankingScope): boolean {
    const d = this.data();
    return !!d && s.scope === d.scope;
  }

  scoreLabel(e: RankingEntry): string {
    if (this.metric === 'trophies') return `${e.trophies ?? 0} 🏆`;
    return `${e.xp} XP`;
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

  photoSrc(e: RankingEntry): string | null {
    return resolveMediaUrl(e.photoUrl) || null;
  }

  initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (!parts.length) return '?';
    if (parts.length === 1) return parts[0].slice(0, 1).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  heldLabel(e: RankingEntry): string | null {
    // Trofeos: no tenure until a dedicated hold history exists.
    if (this.metric !== 'xp') return null;
    return rankingHeldLabel(e.positionHeldSinceUtc);
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.ranking(this.scope, null, this.period, this.metric).subscribe({
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
