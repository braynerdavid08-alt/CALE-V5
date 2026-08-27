import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  LiveAnalyticsDto,
  LiveApi,
  LiveDoubtDto,
  LiveLobbyDto,
  LiveQuestionPayloadDto,
  LiveRankingDto
} from '../../live/api/live.api';

@Component({
  selector: 'app-teacher-live-host-page',
  standalone: true,
  imports: [FormsModule, RouterLink, UiButtonComponent, UiErrorComponent],
  templateUrl: './teacher-live-host.page.html',
  styleUrl: './teacher-live-host.page.css'
})
export class TeacherLiveHostPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(LiveApi);
  private hub: HubConnection | null = null;
  private timerId: ReturnType<typeof setInterval> | null = null;

  readonly lobby = signal<LiveLobbyDto | null>(null);
  readonly ranking = signal<LiveRankingDto | null>(null);
  readonly doubts = signal<LiveDoubtDto[]>([]);
  readonly analytics = signal<LiveAnalyticsDto | null>(null);
  readonly surpriseNotice = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly secondsLeft = signal<number | null>(null);
  readonly answersReceived = signal(0);

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('sessionId'));
      if (!id || Number.isNaN(id)) {
        return;
      }
      void this.hub?.stop();
      this.hub = null;
      this.analytics.set(null);
      this.surpriseNotice.set(null);
      this.reload(id);
      this.connectHub(id);
      this.loadDoubts(id);
    });
  }

  ngOnDestroy(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
    }
    void this.hub?.stop();
  }

  qrUrl(): string {
    const joinUrl = this.lobby()?.joinUrl;
    return joinUrl ? this.api.qrImageUrl(joinUrl) : '';
  }

  showRanking(): boolean {
    const l = this.lobby();
    if (!l) {
      return false;
    }
    return !!(l.config?.showRanking || l.mode === 'Competitive' || l.status === 'Ended');
  }

  control(action: string): void {
    const id = this.lobby()?.sessionId;
    if (!id) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.api.control(id, action).subscribe({
      next: (lobby) => {
        this.loading.set(false);
        this.applyLobby(lobby);
        if (action === 'end' || lobby.status === 'Ended') {
          this.loadAnalytics(id);
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  rematch(): void {
    const id = this.lobby()?.sessionId;
    if (!id) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.api.rematch(id).subscribe({
      next: (res) => {
        this.loading.set(false);
        void this.router.navigate(['/teacher/live', res.newSessionId, 'host']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  resolveDoubt(id: number): void {
    const sessionId = this.lobby()?.sessionId;
    if (!sessionId) {
      return;
    }
    this.api.resolveDoubt(sessionId, id).subscribe({
      next: () => this.loadDoubts(sessionId),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private reload(id: number): void {
    this.api.getHost(id).subscribe({
      next: (lobby) => {
        this.applyLobby(lobby);
        if (lobby.status === 'Ended') {
          this.loadAnalytics(id);
        }
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private loadDoubts(id: number): void {
    this.api.listDoubts(id).subscribe({
      next: (rows) => this.doubts.set(rows),
      error: () => this.doubts.set([])
    });
  }

  private loadAnalytics(id: number): void {
    this.api.analytics(id).subscribe({
      next: (a) => this.analytics.set(a),
      error: () => this.analytics.set(null)
    });
  }

  private applyLobby(lobby: LiveLobbyDto): void {
    this.lobby.set(lobby);
    this.answersReceived.set(lobby.answersReceived);
    if (lobby.ranking) {
      this.ranking.set(lobby.ranking);
    }
    this.syncTimer(lobby.currentQuestion ?? null);
  }

  private syncTimer(q: LiveQuestionPayloadDto | null): void {
    if (this.timerId) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
    if (!q?.closesAt) {
      this.secondsLeft.set(null);
      return;
    }
    const tick = () => {
      const left = Math.max(0, Math.ceil((new Date(q.closesAt!).getTime() - Date.now()) / 1000));
      this.secondsLeft.set(left);
    };
    tick();
    this.timerId = setInterval(tick, 250);
  }

  private connectHub(sessionId: number): void {
    this.hub = this.api.buildHub(true);
    this.hub.on('LobbyUpdated', (payload: LiveLobbyDto) => {
      const prev = this.lobby();
      if (prev?.revealCorrect && prev.currentQuestion && payload.currentQuestion
          && prev.currentQuestion.sessionQuestionId === payload.currentQuestion.sessionQuestionId) {
        this.applyLobby({
          ...payload,
          revealCorrect: true,
          currentQuestion: {
            ...payload.currentQuestion,
            options: prev.currentQuestion.options,
            explanation: prev.currentQuestion.explanation
          }
        });
        return;
      }
      this.applyLobby(payload);
    });
    this.hub.on('QuestionStarted', (payload: LiveQuestionPayloadDto) => {
      const current = this.lobby();
      if (current) {
        this.applyLobby({
          ...current,
          status: 'Running',
          currentQuestion: payload,
          currentQuestionIndex: payload.index,
          questionCount: payload.total,
          answersReceived: 0
        });
      }
      this.answersReceived.set(0);
      this.surpriseNotice.set(payload.isSurprise ? '¡Pregunta sorpresa!' : null);
      this.syncTimer(payload);
    });
    this.hub.on('QuestionClosed', () => {
      const current = this.lobby();
      if (current?.currentQuestion) {
        this.syncTimer({ ...current.currentQuestion, closesAt: new Date().toISOString() });
      }
    });
    this.hub.on('AnswerReceived', (payload: { answersReceived: number }) => {
      this.answersReceived.set(payload.answersReceived ?? 0);
    });
    this.hub.on('RevealUpdated', (payload: LiveQuestionPayloadDto) => {
      const current = this.lobby();
      if (current) {
        this.applyLobby({ ...current, currentQuestion: payload, revealCorrect: true });
      }
    });
    this.hub.on('RankingUpdated', (payload: LiveRankingDto) => this.ranking.set(payload));
    this.hub.on('DoubtsUpdated', (payload: LiveDoubtDto[]) => this.doubts.set(payload ?? []));
    this.hub.on('SurpriseQueued', (payload: { message?: string; questionCount?: number }) => {
      this.surpriseNotice.set(payload?.message || 'Pregunta sorpresa en cola');
      const current = this.lobby();
      if (current && payload?.questionCount) {
        this.lobby.set({ ...current, questionCount: payload.questionCount });
      }
    });
    this.hub.on('SessionEnded', () => {
      const current = this.lobby();
      if (current) {
        this.applyLobby({ ...current, status: 'Ended' });
        this.loadAnalytics(current.sessionId);
      }
    });
    void this.hub.start().then(() => this.hub!.invoke('JoinAsHost', sessionId));
  }

  participationPercent(): number {
    const l = this.lobby();
    if (!l || l.participantCount === 0) {
      return 0;
    }
    return Math.round((100 * this.answersReceived()) / l.participantCount);
  }
}
