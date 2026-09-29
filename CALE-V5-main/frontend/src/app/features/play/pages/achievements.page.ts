import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiIconComponent } from '../../../shared/ui/ui-icon.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { Achievements, Badge, PlayApi } from '../api/play.api';
import { PlayBadgesToastComponent } from '../components/play-badges-toast.component';
import { PlayTopbarComponent } from '../components/play-topbar.component';
import { PlayFxService } from '../play-fx.service';
import { shareCard } from '../share-card';

@Component({
  selector: 'app-achievements-page',
  standalone: true,
  imports: [
    DatePipe,
    UiButtonComponent,
    UiIconComponent,
    UiLoadingComponent,
    PlayBadgesToastComponent,
    PlayTopbarComponent
  ],
  styleUrl: './play-page.css',
  styles: [`
    .level { display: grid; gap: 0.6rem; }
    .level-top { display: flex; align-items: center; gap: 1rem; }
    .level-num {
      flex: none;
      display: grid;
      place-items: center;
      width: 4.2rem;
      height: 4.2rem;
      border-radius: 50%;
      font-size: 1.6rem;
      font-weight: 800;
      color: #fff;
      background: linear-gradient(135deg, var(--color-primary), #22c55e);
    }
    .level-top h2 { margin: 0; }
    .level-top p { margin: 0.15rem 0 0; color: var(--color-text-secondary); }
    .badges { display: grid; grid-template-columns: repeat(auto-fill, minmax(12.5rem, 1fr)); gap: 0.85rem; }
    .badge {
      display: grid;
      gap: 0.4rem;
      justify-items: center;
      text-align: center;
      padding: 1rem 0.85rem;
      border-radius: var(--radius-lg);
      border: 1px solid var(--color-border);
      background: var(--color-surface);
    }
    .badge.earned { border-color: #f59e0b; box-shadow: 0 6px 18px rgba(245, 158, 11, 0.18); }
    .badge-icon {
      display: grid;
      place-items: center;
      width: 3.6rem;
      height: 3.6rem;
      border-radius: 50%;
      color: var(--color-text-secondary);
      background: var(--color-chip);
    }
    .badge.earned .badge-icon { color: #b45309; background: #fef3c7; }
    .badge:not(.earned) { opacity: 0.8; }
    .badge strong { font-size: var(--text-md); }
    .badge p { margin: 0; font-size: var(--text-sm); color: var(--color-text-secondary); }
    .mini-bar { width: 100%; height: 0.4rem; border-radius: 999px; background: var(--color-chip); overflow: hidden; }
    .mini-bar span { display: block; height: 100%; background: var(--color-primary); }
    .count { font-size: var(--text-xs); color: var(--color-text-secondary); }
    h2.section { margin: 0 0 0.85rem; font-size: var(--text-lg); }
  `],
  template: `
    <section class="play-page wide">
      <play-topbar title="Mis logros" subtitle="Gana experiencia practicando y desbloquea insignias." />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (loading()) {
        <ui-loading />
      } @else {
        @if (data(); as d) {
        <div class="play-card level">
          <div class="level-top">
            <span class="level-num">{{ d.level.level }}</span>
            <div>
              <h2>{{ d.level.name }}</h2>
              <p>
                {{ d.level.xp }} XP
                @if (d.level.nextLevelXp) {
                  · faltan {{ d.level.nextLevelXp - d.level.xp }} XP para «{{ d.level.nextLevelName }}»
                } @else {
                  · ¡Nivel máximo!
                }
              </p>
            </div>
          </div>
          <div class="play-progress">
            <span class="bar"><span [style.width.%]="d.level.progressPercent"></span></span>
            <span>{{ d.level.progressPercent }}%</span>
          </div>
          <div class="play-actions">
            <ui-button type="button" variant="secondary" [loading]="sharing()" (click)="share()">Compartir mi nivel</ui-button>
          </div>
          @if (shareMsg()) {
            <p class="muted">{{ shareMsg() }}</p>
          }
        </div>

        <div class="play-card">
          <h2 class="section">Insignias · {{ earnedCount() }} de {{ d.badges.length }}</h2>
          <div class="badges">
            @for (b of d.badges; track b.code) {
              <article class="badge" [class.earned]="b.earned">
                <span class="badge-icon" aria-hidden="true"><ui-icon [name]="b.icon" /></span>
                <strong>{{ b.title }}</strong>
                <p>{{ b.description }}</p>
                @if (b.earned) {
                  <span class="count">Ganada el {{ b.earnedAt | date: 'd MMM y' }}</span>
                } @else {
                  <span class="mini-bar"><span [style.width.%]="pct(b)"></span></span>
                  <span class="count">{{ b.current }} / {{ b.target }}</span>
                }
              </article>
            }
          </div>
        </div>
        }
      }
      <play-badges-toast [badges]="newBadges()" (closed)="newBadges.set([])" />
    </section>
  `
})
export class AchievementsPage implements OnInit {
  private readonly api = inject(PlayApi);
  private readonly fx = inject(PlayFxService);
  private readonly session = inject(SessionStore);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly data = signal<Achievements | null>(null);
  readonly newBadges = signal<Badge[]>([]);
  readonly sharing = signal(false);
  readonly shareMsg = signal<string | null>(null);

  readonly earnedCount = computed(() => this.data()?.badges.filter((b) => b.earned).length ?? 0);

  ngOnInit(): void {
    this.api.achievements().subscribe({
      next: (d) => {
        this.data.set(d);
        this.loading.set(false);
        if (d.newBadges.length) {
          this.newBadges.set(d.newBadges);
          this.fx.play('badge');
          this.fx.confetti(90);
        }
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  pct(b: Badge): number {
    return b.target ? Math.min(100, Math.round((b.current / b.target) * 100)) : 0;
  }

  async share(): Promise<void> {
    const d = this.data();
    if (!d) return;
    this.sharing.set(true);
    this.shareMsg.set(null);
    try {
      const result = await shareCard({
        kicker: `Nivel ${d.level.level}`,
        headline: `${d.level.xp} XP`,
        title: d.level.name,
        details: [`${this.earnedCount()} insignias desbloqueadas`],
        name: this.session.user()?.name,
        tone: 'primary'
      });
      if (result === 'downloaded') this.shareMsg.set('Imagen descargada. Ya puedes subirla a tus redes.');
    } catch {
      this.shareMsg.set('No se pudo generar la imagen en este navegador.');
    } finally {
      this.sharing.set(false);
    }
  }
}
