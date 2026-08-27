import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { LiveApi, LiveLobbyDto, LiveQuestionPayloadDto } from '../../live/api/live.api';

@Component({
  selector: 'app-teacher-live-host-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiErrorComponent],
  templateUrl: './teacher-live-host.page.html',
  styleUrl: './teacher-live-host.page.css'
})
export class TeacherLiveHostPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(LiveApi);
  private hub: HubConnection | null = null;
  private timerId: ReturnType<typeof setInterval> | null = null;

  readonly lobby = signal<LiveLobbyDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly secondsLeft = signal<number | null>(null);
  readonly answersReceived = signal(0);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('sessionId'));
    this.reload(id);
    this.connectHub(id);
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
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private reload(id: number): void {
    this.api.getHost(id).subscribe({
      next: (lobby) => this.applyLobby(lobby),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private applyLobby(lobby: LiveLobbyDto): void {
    this.lobby.set(lobby);
    this.answersReceived.set(lobby.answersReceived);
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
          answersReceived: 0
        });
      }
      this.answersReceived.set(0);
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
    this.hub.on('SessionEnded', () => {
      const current = this.lobby();
      if (current) {
        this.applyLobby({ ...current, status: 'Ended' });
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
