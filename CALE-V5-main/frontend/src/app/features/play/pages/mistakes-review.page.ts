import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { AnswerFeedback, Badge, Mistakes, PlayApi } from '../api/play.api';
import { PlayBadgesToastComponent } from '../components/play-badges-toast.component';
import { PlayQuestionComponent } from '../components/play-question.component';
import { PlayTopbarComponent } from '../components/play-topbar.component';
import { PlayFxService } from '../play-fx.service';

@Component({
  selector: 'app-mistakes-review-page',
  standalone: true,
  imports: [
    DatePipe,
    UiButtonComponent,
    UiEmptyComponent,
    UiLoadingComponent,
    PlayBadgesToastComponent,
    PlayQuestionComponent,
    PlayTopbarComponent
  ],
  styleUrl: './play-page.css',
  template: `
    <section class="play-page">
      <play-topbar
        title="Repaso de mis errores"
        subtitle="Las preguntas que fallaste vuelven hasta que las domines. Si aciertas, vuelven más tarde; si fallas, vuelven pronto." />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (loading()) {
        <ui-loading />
      } @else {
        @if (data(); as d) {
        <div class="chip-row">
          <span class="chip warn">📌 Para hoy: {{ d.dueCount }}</span>
          <span class="chip">⏳ En repaso: {{ d.pendingCount }}</span>
          <span class="chip good">✅ Dominadas: {{ masteredCount() }}</span>
        </div>

        @if (question(); as q) {
          <div class="play-card">
            <div class="play-progress">
              <span>{{ index() + 1 }} de {{ d.questions.length }}</span>
              <span class="bar"><span [style.width.%]="progress()"></span></span>
            </div>
            <play-question
              [question]="q"
              [feedback]="feedback()"
              [selectedId]="selected()"
              [busy]="busy()"
              (answered)="answer($event)" />
            @if (feedback()) {
              @if (masteredNow()) {
                <p class="chip good">🎉 ¡Pregunta dominada! Ya no volverá a tu repaso.</p>
              }
              <div class="play-actions">
                <ui-button type="button" (click)="next()">
                  {{ index() + 1 < d.questions.length ? 'Siguiente' : 'Terminar repaso' }}
                </ui-button>
              </div>
            }
          </div>
        } @else if (sessionDone()) {
          <div class="play-card result-hero">
            <p class="big">{{ sessionCorrect() }}/{{ answeredInSession() }}</p>
            <h2>Repaso terminado</h2>
            <p>Cada acierto aleja la pregunta unos días. Sigue así y las dominarás todas.</p>
            <div class="play-actions center">
              <ui-button type="button" variant="secondary" (click)="load()">Buscar más para repasar</ui-button>
              <ui-button routerLink="/student">Volver al inicio</ui-button>
            </div>
          </div>
        } @else if (d.pendingCount > 0) {
          <ui-empty
            title="Por ahora no tienes preguntas pendientes"
            [message]="d.nextDueAt ? 'La próxima vuelve el ' + (d.nextDueAt | date: 'd MMM, h:mm a') + '.' : 'Vuelve más tarde.'" />
        } @else {
          <ui-empty
            title="¡No tienes errores por repasar!"
            message="Cuando falles una pregunta en un simulacro o en el reto diario, aparecerá aquí." />
          <div class="play-actions center">
            <ui-button routerLink="/student/play/daily">Hacer el reto diario</ui-button>
          </div>
        }
        }
      }
      <play-badges-toast [badges]="newBadges()" (closed)="newBadges.set([])" />
    </section>
  `
})
export class MistakesReviewPage implements OnInit {
  private readonly api = inject(PlayApi);
  private readonly fx = inject(PlayFxService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly data = signal<Mistakes | null>(null);
  readonly index = signal(0);
  readonly selected = signal<number | null>(null);
  readonly feedback = signal<AnswerFeedback | null>(null);
  readonly masteredNow = signal(false);
  readonly busy = signal(false);
  readonly sessionDone = signal(false);
  readonly sessionCorrect = signal(0);
  readonly answeredInSession = signal(0);
  readonly masteredCount = signal(0);
  readonly newBadges = signal<Badge[]>([]);

  readonly question = computed(() => (this.sessionDone() ? null : this.data()?.questions[this.index()] ?? null));
  readonly progress = computed(() => {
    const total = this.data()?.questions.length ?? 0;
    return total ? ((this.index() + (this.feedback() ? 1 : 0)) / total) * 100 : 0;
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.mistakes().subscribe({
      next: (d) => {
        this.data.set(d);
        this.masteredCount.set(d.masteredCount);
        this.index.set(0);
        this.sessionDone.set(false);
        this.sessionCorrect.set(0);
        this.answeredInSession.set(0);
        this.feedback.set(null);
        this.selected.set(null);
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
    this.api.answerMistake(q.id, optionId).subscribe({
      next: (res) => {
        this.busy.set(false);
        this.feedback.set(res);
        this.masteredNow.set(res.mastered);
        this.answeredInSession.update((n) => n + 1);
        if (res.correct) this.sessionCorrect.update((n) => n + 1);
        if (res.mastered) this.masteredCount.update((n) => n + 1);
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
    const total = this.data()?.questions.length ?? 0;
    this.feedback.set(null);
    this.selected.set(null);
    this.masteredNow.set(false);
    if (this.index() + 1 < total) {
      this.index.update((i) => i + 1);
      return;
    }
    this.sessionDone.set(true);
    if (this.answeredInSession() && this.sessionCorrect() === this.answeredInSession()) {
      this.fx.celebrate();
    }
  }
}
