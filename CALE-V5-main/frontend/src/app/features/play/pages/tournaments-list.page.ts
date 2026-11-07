import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, catchError } from 'rxjs';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { pollWhileVisible } from '../../../core/rxjs/poll-while-visible';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import {
  TournamentApi,
  TournamentListItem,
  tournamentStatusLabel,
  tournamentStatusTone
} from '../api/tournament.api';
import { PlayTopbarComponent } from '../components/play-topbar.component';

const REFRESH_MS = 10_000;

@Component({
  selector: 'app-tournaments-list-page',
  standalone: true,
  imports: [DatePipe, RouterLink, UiButtonComponent, PlayTopbarComponent],
  styleUrls: ['./play-page.css', './tournament-page.css'],
  template: `
    <section class="play-page wide">
      <play-topbar
        title="Torneos Luz Verde"
        subtitle="Compite en llaves de eliminación directa, gana trofeos y sube de nivel."
        [back]="homeRoute()"
        backLabel="Volver al inicio" />

      <div class="t-toolbar">
        @if (isStudent()) {
          <ui-button [routerLink]="base + '/profile'" variant="secondary">🏆 Mi perfil competitivo</ui-button>
        } @else {
          <span></span>
        }
        @if (isTeacher()) {
          <ui-button [routerLink]="base + '/new'">Crear torneo</ui-button>
        }
      </div>

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (loading()) {
        <div class="play-card result-hero"><p class="pulse">Cargando torneos…</p></div>
      } @else if (!tournaments().length) {
        <div class="play-card result-hero">
          <p class="big">🏆</p>
          <h2>Aún no hay torneos</h2>
          <p>
            @if (isTeacher()) {
              Crea el primero y comparte el enlace con tus estudiantes.
            } @else {
              Cuando tu instructor o escuela abra un torneo, aparecerá aquí.
            }
          </p>
        </div>
      } @else {
        <div class="t-grid">
          @for (t of tournaments(); track t.id) {
            <article class="play-card t-card">
              <div class="t-head">
                <h2>{{ t.name }}</h2>
                <span class="chip" [class.good]="tone(t.status) === 'good'" [class.warn]="tone(t.status) === 'warn'">
                  {{ label(t.status) }}
                </span>
              </div>
              @if (t.description) {
                <p>{{ t.description }}</p>
              }
              <div class="t-meta">
                <span>📅 {{ t.scheduledAt | date: 'd MMM y, h:mm a' }}</span>
                <span>👥 {{ t.registeredCount }}/{{ t.capacity }} inscritos</span>
                <span>🟢 {{ t.connectedCount }} conectados</span>
                <span>Organiza: {{ t.organizerName }}</span>
              </div>
              <div class="play-actions">
                @if (isStudent()) {
                  @if (t.myMatchId && t.status === 'in_progress') {
                    <ui-button [routerLink]="base + '/match/' + t.myMatchId">Ir a mi partida</ui-button>
                  } @else if (canJoin(t)) {
                    <ui-button [loading]="joiningId() === t.id" (click)="join(t)">Inscribirme</ui-button>
                  } @else if (t.amIRegistered) {
                    <span class="chip good">✔ Inscrito</span>
                  }
                }
                <ui-button [routerLink]="base + '/' + t.id" variant="secondary">
                  {{ isTeacher() ? 'Gestionar' : 'Ver sala' }}
                </ui-button>
                @if (t.status === 'in_progress' || t.status === 'finished') {
                  <ui-button [routerLink]="base + '/' + t.id + '/bracket'" variant="ghost">Ver llaves</ui-button>
                }
              </div>
            </article>
          }
        </div>
      }
    </section>
  `
})
export class TournamentsListPage {
  private readonly api = inject(TournamentApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly base: string =
    inject(ActivatedRoute).snapshot.data['base'] ?? '/student/play/tournaments';

  readonly tournaments = signal<TournamentListItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly joiningId = signal<number | null>(null);

  readonly isStudent = computed(() => this.session.user()?.role === 'Student');
  readonly isTeacher = computed(() => this.session.user()?.role === 'Teacher');
  readonly homeRoute = computed(() => this.session.homeRoute());

  readonly label = tournamentStatusLabel;
  readonly tone = tournamentStatusTone;

  constructor() {
    pollWhileVisible(REFRESH_MS, () =>
      this.api.list().pipe(
        catchError((err) => {
          this.loading.set(false);
          this.error.set(mapApiError(err));
          return EMPTY;
        })
      )
    )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((items) => {
        this.tournaments.set(items);
        this.loading.set(false);
        this.error.set(null);
      });
  }

  canJoin(t: TournamentListItem): boolean {
    return t.status === 'registration_open' && !t.amIRegistered && t.registeredCount < t.capacity;
  }

  join(t: TournamentListItem): void {
    if (this.joiningId() !== null) return;
    this.joiningId.set(t.id);
    this.error.set(null);
    this.api.join(t.id).subscribe({
      next: () => {
        this.joiningId.set(null);
        void this.router.navigate([this.base, t.id]);
      },
      error: (err) => {
        this.joiningId.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }
}
