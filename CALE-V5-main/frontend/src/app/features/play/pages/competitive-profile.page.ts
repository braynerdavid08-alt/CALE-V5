import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiIconComponent } from '../../../shared/ui/ui-icon.component';
import { CompetitiveProfile, TournamentApi } from '../api/tournament.api';
import { PlayTopbarComponent } from '../components/play-topbar.component';

@Component({
  selector: 'app-competitive-profile-page',
  standalone: true,
  imports: [DatePipe, UiButtonComponent, UiIconComponent, PlayTopbarComponent],
  styleUrls: ['./play-page.css', './tournament-page.css'],
  template: `
    <section class="play-page">
      <play-topbar
        title="Mi perfil competitivo"
        subtitle="Tus trofeos, victorias y nivel en los torneos Luz Verde."
        back="/student/play/tournaments"
        backLabel="Volver a torneos" />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (loading()) {
        <div class="play-card result-hero"><p class="pulse">Cargando perfil…</p></div>
      } @else {
        @if (profile(); as p) {
        <div class="play-card t-card">
          <div class="t-head">
            <h2>Nivel {{ p.level.level }} · {{ p.level.name }}</h2>
            @if (p.generalRank) {
              <span class="chip good">Puesto #{{ p.generalRank }} general</span>
            }
          </div>
          <div class="level-bar"><span [style.width.%]="p.level.progressPercent"></span></div>
          <p>
            {{ p.level.xp }} XP
            @if (p.level.nextLevelXp && p.level.nextLevelName) {
              · faltan {{ p.level.nextLevelXp - p.level.xp }} XP para «{{ p.level.nextLevelName }}»
            }
          </p>
        </div>

        <div class="stat-grid">
          <div class="stat"><strong>{{ p.trophyCount }}</strong><small>Trofeos</small></div>
          <div class="stat"><strong>{{ p.tournamentsWon }}</strong><small>Torneos ganados</small></div>
          <div class="stat"><strong>{{ p.tournamentsPlayed }}</strong><small>Torneos jugados</small></div>
          <div class="stat"><strong>{{ p.finalsReached }}</strong><small>Finales</small></div>
          <div class="stat"><strong>{{ p.matchWins }}</strong><small>Victorias</small></div>
          <div class="stat"><strong>{{ p.matchLosses }}</strong><small>Derrotas</small></div>
          <div class="stat"><strong>{{ winRate() }}%</strong><small>Efectividad</small></div>
          <div class="stat"><strong>{{ p.achievementCount }}</strong><small>Logros</small></div>
        </div>

        <div class="play-card t-card">
          <h2>Trofeos</h2>
          @if (!p.trophies.length) {
            <p>Todavía no tienes trofeos. ¡Inscríbete en un torneo y compite por el primero!</p>
          } @else {
            <ul class="trophies">
              @for (t of p.trophies; track t.id) {
                <li>
                  <span class="emoji" aria-hidden="true">🏆</span>
                  <span>
                    <strong>{{ t.tournamentName }}</strong>
                    <small>{{ t.championResult }} · {{ t.obtainedAt | date: 'd MMM y' }}</small>
                  </span>
                </li>
              }
            </ul>
          }
        </div>

        @if (earnedBadges().length) {
          <div class="play-card t-card">
            <h2>Insignias</h2>
            <div class="chip-row">
              @for (b of earnedBadges(); track b.code) {
                <span class="chip good" [attr.title]="b.description">
                  <ui-icon [name]="b.icon" /> {{ b.title }}
                </span>
              }
            </div>
          </div>
        }

        <div class="play-actions center">
          <ui-button routerLink="/student/play/tournaments">Buscar un torneo</ui-button>
          <ui-button routerLink="/student/play/ranking" variant="ghost">Ver ranking</ui-button>
        </div>
        }
      }
    </section>
  `
})
export class CompetitiveProfilePage implements OnInit {
  private readonly api = inject(TournamentApi);

  readonly profile = signal<CompetitiveProfile | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly earnedBadges = computed(() => this.profile()?.badges.filter((b) => b.earned) ?? []);

  readonly winRate = computed(() => {
    const p = this.profile();
    if (!p) return 0;
    const total = p.matchWins + p.matchLosses;
    return total ? Math.round((p.matchWins / total) * 100) : 0;
  });

  ngOnInit(): void {
    this.api.profile().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
