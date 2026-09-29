import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { AnswerFeedback, Badge, DailyChallenge, PlayApi } from '../api/play.api';
import { PlayBadgesToastComponent } from '../components/play-badges-toast.component';
import { PlayQuestionComponent } from '../components/play-question.component';
import { PlayTopbarComponent } from '../components/play-topbar.component';
import { PlayFxService } from '../play-fx.service';
import { shareCard } from '../share-card';

@Component({
  selector: 'app-daily-challenge-page',
  standalone: true,
  imports: [
    UiButtonComponent,
    UiLoadingComponent,
    PlayBadgesToastComponent,
    PlayQuestionComponent,
    PlayTopbarComponent
  ],
  styleUrl: './play-page.css',
  template: `
    <section class="play-page">
      <play-topbar
        title="Reto diario"
        subtitle="5 preguntas nuevas cada día. Responde todas para mantener tu racha." />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (loading()) {
        <ui-loading />
      } @else {
        @if (challenge(); as c) {
        <div class="chip-row">
          <span class="chip" [class.good]="c.streak.activeToday">🔥 Racha: {{ c.streak.current }} {{ c.streak.current === 1 ? 'día' : 'días' }}</span>
          <span class="chip">🏆 Mejor racha: {{ c.streak.best }}</span>
        </div>

        @if (!finished()) {
          @if (question(); as q) {
            <div class="play-card">
              <div class="play-progress">
                <span>Pregunta {{ index() + 1 }} de {{ c.questions.length }}</span>
                <span class="bar"><span [style.width.%]="progress()"></span></span>
              </div>
              <play-question
                [question]="q"
                [feedback]="feedback()"
                [selectedId]="selected()"
                [busy]="busy()"
                (answered)="answer($event)" />
              @if (feedback()) {
                <div class="play-actions">
                  <ui-button type="button" (click)="next()">
                    {{ index() + 1 < c.questions.length ? 'Siguiente pregunta' : 'Ver resultado' }}
                  </ui-button>
                </div>
              }
            </div>
          }
        } @else {
          <div class="play-card result-hero">
            <p class="big">{{ correctCount() }}/{{ c.questions.length }}</p>
            <h2>{{ resultTitle() }}</h2>
            <p>
              Tu racha va en <strong>{{ streakCurrent() }} {{ streakCurrent() === 1 ? 'día' : 'días' }}</strong>.
              Vuelve mañana por un reto nuevo.
            </p>
            <div class="play-actions center">
              @if (correctCount() < c.questions.length) {
                <ui-button routerLink="/student/play/mistakes" variant="secondary">Repasar mis errores</ui-button>
              }
              <ui-button type="button" variant="secondary" [loading]="sharing()" (click)="share()">Compartir</ui-button>
              <ui-button routerLink="/student">Volver al inicio</ui-button>
            </div>
            @if (shareMsg()) {
              <p class="muted">{{ shareMsg() }}</p>
            }
          </div>
        }
        }
      }
      <play-badges-toast [badges]="newBadges()" (closed)="newBadges.set([])" />
    </section>
  `
})
export class DailyChallengePage implements OnInit {
  private readonly api = inject(PlayApi);
  private readonly fx = inject(PlayFxService);
  private readonly session = inject(SessionStore);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly challenge = signal<DailyChallenge | null>(null);
  readonly index = signal(0);
  readonly selected = signal<number | null>(null);
  readonly feedback = signal<AnswerFeedback | null>(null);
  readonly busy = signal(false);
  readonly finished = signal(false);
  readonly correctCount = signal(0);
  readonly streakCurrent = signal(0);
  readonly newBadges = signal<Badge[]>([]);
  readonly sharing = signal(false);
  readonly shareMsg = signal<string | null>(null);

  readonly question = computed(() => this.challenge()?.questions[this.index()] ?? null);
  readonly progress = computed(() => {
    const total = this.challenge()?.questions.length ?? 0;
    return total ? ((this.index() + (this.feedback() ? 1 : 0)) / total) * 100 : 0;
  });
  readonly resultTitle = computed(() => {
    const total = this.challenge()?.questions.length ?? 5;
    const correct = this.correctCount();
    if (correct === total) return '¡Reto perfecto!';
    if (correct >= total - 1) return '¡Muy bien!';
    if (correct >= Math.ceil(total / 2)) return 'Buen trabajo';
    return 'Reto completado';
  });

  ngOnInit(): void {
    this.api.daily().subscribe({
      next: (c) => {
        this.challenge.set(c);
        this.correctCount.set(c.correctCount);
        this.streakCurrent.set(c.streak.current);
        const answered = new Set(c.answers.map((a) => a.questionId));
        const firstOpen = c.questions.findIndex((q) => !answered.has(q.id));
        this.finished.set(c.completed || firstOpen < 0);
        this.index.set(Math.max(0, firstOpen));
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  answer(optionId: number): void {
    const q = this.question();
    if (!q || this.busy() || this.feedback()) return;
    this.selected.set(optionId);
    this.busy.set(true);
    this.api.answerDaily(q.id, optionId).subscribe({
      next: (res) => {
        this.busy.set(false);
        this.feedback.set(res);
        this.correctCount.set(res.correctCount);
        this.streakCurrent.set(res.streak.current);
        this.fx.play(res.correct ? 'correct' : 'wrong');
        if (res.newBadges.length) {
          this.newBadges.set(res.newBadges);
          this.fx.play('badge');
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.selected.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  next(): void {
    const total = this.challenge()?.questions.length ?? 0;
    this.feedback.set(null);
    this.selected.set(null);
    if (this.index() + 1 < total) {
      this.index.update((i) => i + 1);
      return;
    }
    this.finished.set(true);
    if (this.correctCount() >= total - 1) {
      this.fx.celebrate();
    } else {
      this.fx.play('start');
    }
  }

  async share(): Promise<void> {
    const total = this.challenge()?.questions.length ?? 5;
    this.sharing.set(true);
    this.shareMsg.set(null);
    try {
      const result = await shareCard({
        kicker: 'Reto diario',
        headline: `🔥 ${this.streakCurrent()} ${this.streakCurrent() === 1 ? 'día' : 'días'}`,
        title: `Hoy acerté ${this.correctCount()} de ${total}`,
        details: ['Practicando todos los días para el examen'],
        name: this.session.user()?.name,
        tone: 'warning'
      });
      if (result === 'downloaded') this.shareMsg.set('Imagen descargada. Ya puedes subirla a tus redes.');
    } catch {
      this.shareMsg.set('No se pudo generar la imagen en este navegador.');
    } finally {
      this.sharing.set(false);
    }
  }
}
