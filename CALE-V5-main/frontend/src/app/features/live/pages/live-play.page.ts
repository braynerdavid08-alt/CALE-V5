import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  LiveApi,
  LiveDoubtDto,
  LiveLobbyDto,
  LiveQuestionPayloadDto,
  LiveRankingDto,
  sanitizeLiveLobby
} from '../api/live.api';
import { computeSecondsLeft } from '../live-timer.util';
import { readLiveParticipant, saveLiveParticipant } from './live-join.page';

@Component({
  selector: 'app-live-play-page',
  standalone: true,
  imports: [FormsModule, RouterLink, UiButtonComponent, UiErrorComponent],
  templateUrl: './live-play.page.html',
  styleUrl: './live-play.page.css'
})
export class LivePlayPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(LiveApi);
  private hub: HubConnection | null = null;
  private timerId: ReturnType<typeof setInterval> | null = null;
  private timerQuestionId: number | null = null;
  private localDeadlineMs = 0;

  readonly lobby = signal<LiveLobbyDto | null>(null);
  readonly ranking = signal<LiveRankingDto | null>(null);
  readonly doubts = signal<LiveDoubtDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly secondsLeft = signal<number | null>(null);
  readonly selectedOptionId = signal<number | null>(null);
  readonly submitted = signal(false);
  readonly displayName = signal('');
  readonly doubtText = signal('');
  readonly lastPoints = signal<number | null>(null);
  readonly rematchCode = signal<string | null>(null);

  private sessionId = 0;
  private token = '';

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('sessionId'));
      if (!id || Number.isNaN(id)) {
        return;
      }
      const saved = readLiveParticipant(id);
      if (!saved) {
        void this.router.navigate(['/live/join']);
        return;
      }
      void this.hub?.stop();
      this.hub = null;
      this.sessionId = id;
      this.token = saved.participantToken;
      this.displayName.set(saved.displayName);
      this.rematchCode.set(null);
      this.reload();
      this.connectHub();
      this.loadDoubts();
    });
  }

  ngOnDestroy(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
    }
    void this.hub?.stop();
  }

  showRanking(): boolean {
    const l = this.lobby();
    if (!l) {
      return false;
    }
    return !!(l.config?.showRanking || l.mode === 'Competitive' || l.status === 'Ended');
  }

  select(optionId: number): void {
    if (this.submitted()) {
      return;
    }
    this.selectedOptionId.set(optionId);
  }

  confirm(): void {
    const q = this.lobby()?.currentQuestion;
    const optionId = this.selectedOptionId();
    if (!q || optionId == null || this.submitted()) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.api.answer(this.sessionId, q.sessionQuestionId, this.token, optionId).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.submitted.set(true);
        if (typeof res?.points === 'number') {
          this.lastPoints.set(res.points);
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  sendDoubt(): void {
    const text = this.doubtText().trim();
    if (text.length < 3) {
      return;
    }
    this.api.postDoubt(this.sessionId, this.token, text).subscribe({
      next: () => {
        this.doubtText.set('');
        this.loadDoubts();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  vote(doubtId: number): void {
    this.api.voteDoubt(this.sessionId, doubtId, this.token).subscribe({
      next: () => this.loadDoubts(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  joinRematch(): void {
    const code = this.rematchCode();
    if (!code) {
      return;
    }
    this.loading.set(true);
    this.api.join(code, this.displayName()).subscribe({
      next: (res) => {
        this.loading.set(false);
        saveLiveParticipant(res);
        void this.router.navigate(['/live/play', res.sessionId]);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private reload(): void {
    this.api.getPlay(this.sessionId, this.token).subscribe({
      next: (lobby) => this.applyLobby(lobby),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private loadDoubts(): void {
    this.api.listDoubts(this.sessionId, this.token).subscribe({
      next: (rows) => this.doubts.set(rows),
      error: () => this.doubts.set([])
    });
  }

  private applyLobby(lobby: LiveLobbyDto): void {
    const prevId = this.lobby()?.currentQuestion?.sessionQuestionId;
    const safe = lobby.revealCorrect ? lobby : sanitizeLiveLobby(lobby);
    this.lobby.set(safe);
    if (lobby.ranking) {
      this.ranking.set(lobby.ranking);
    }
    if (lobby.currentQuestion?.sessionQuestionId !== prevId) {
      this.selectedOptionId.set(null);
      this.submitted.set(false);
      this.lastPoints.set(null);
    }
    this.syncTimer(lobby.currentQuestion ?? null);
  }

  private syncTimer(q: LiveQuestionPayloadDto | null): void {
    if (this.timerId) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
    if (!q) {
      this.secondsLeft.set(null);
      this.timerQuestionId = null;
      this.localDeadlineMs = 0;
      return;
    }

    const maxSecs = Math.max(5, q.secondsPerQuestion ?? this.lobby()?.config?.secondsPerQuestion ?? 30);
    if (q.sessionQuestionId !== this.timerQuestionId) {
      this.timerQuestionId = q.sessionQuestionId;
      const synced = computeSecondsLeft(q, this.lobby()?.config?.secondsPerQuestion);
      const startLeft = synced ?? maxSecs;
      this.localDeadlineMs = Date.now() + startLeft * 1000;
    }

    const tick = () => {
      if (this.lobby()?.status === 'Paused') {
        return;
      }
      const synced = computeSecondsLeft(q, this.lobby()?.config?.secondsPerQuestion);
      const left = synced ?? Math.max(0, Math.ceil((this.localDeadlineMs - Date.now()) / 1000));
      this.secondsLeft.set(left);
    };
    tick();
    this.timerId = setInterval(tick, 250);
  }

  private connectHub(): void {
    this.hub = this.api.buildHub(false);
    this.hub.on('LobbyUpdated', (payload: LiveLobbyDto) => this.applyLobby(payload));
    this.hub.on('QuestionStarted', (payload: LiveQuestionPayloadDto) => {
      const current = this.lobby();
      if (current) {
        this.applyLobby({
          ...current,
          status: 'Running',
          revealCorrect: false,
          currentQuestion: payload,
          currentQuestionIndex: payload.index,
          questionCount: payload.total
        });
      }
      this.selectedOptionId.set(null);
      this.submitted.set(false);
      this.lastPoints.set(null);
      this.syncTimer(payload);
    });
    this.hub.on('QuestionClosed', () => this.submitted.set(true));
    this.hub.on('RevealUpdated', (payload: LiveQuestionPayloadDto) => {
      const current = this.lobby();
      if (current) {
        this.applyLobby({ ...current, currentQuestion: payload, revealCorrect: true });
      }
    });
    this.hub.on('AnswerReceived', () => { /* counts handled on host */ });
    this.hub.on('RankingUpdated', (payload: LiveRankingDto) => this.ranking.set(payload));
    this.hub.on('DoubtsUpdated', (payload: LiveDoubtDto[]) => this.doubts.set(payload ?? []));
    this.hub.on('RematchReady', (payload: { joinCode?: string }) => {
      if (payload?.joinCode) {
        this.rematchCode.set(payload.joinCode);
      }
    });
    this.hub.on('SessionEnded', () => {
      const current = this.lobby();
      if (current) {
        this.applyLobby({ ...current, status: 'Ended', currentQuestion: null });
      }
    });
    void this.hub.start().then(() =>
      this.hub!.invoke('JoinAsParticipant', this.sessionId, this.token)
    );
  }
}
