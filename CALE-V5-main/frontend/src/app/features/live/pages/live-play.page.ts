import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { LiveApi, LiveLobbyDto, LiveQuestionPayloadDto } from '../api/live.api';
import { readLiveParticipant } from './live-join.page';

@Component({
  selector: 'app-live-play-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiErrorComponent],
  templateUrl: './live-play.page.html',
  styleUrl: './live-play.page.css'
})
export class LivePlayPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(LiveApi);
  private hub: HubConnection | null = null;
  private timerId: ReturnType<typeof setInterval> | null = null;

  readonly lobby = signal<LiveLobbyDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly secondsLeft = signal<number | null>(null);
  readonly selectedOptionId = signal<number | null>(null);
  readonly submitted = signal(false);
  readonly displayName = signal('');

  private sessionId = 0;
  private token = '';

  ngOnInit(): void {
    this.sessionId = Number(this.route.snapshot.paramMap.get('sessionId'));
    const saved = readLiveParticipant(this.sessionId);
    if (!saved) {
      void this.router.navigate(['/live/join']);
      return;
    }
    this.token = saved.participantToken;
    this.displayName.set(saved.displayName);
    this.reload();
    this.connectHub();
  }

  ngOnDestroy(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
    }
    void this.hub?.stop();
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
      next: () => {
        this.loading.set(false);
        this.submitted.set(true);
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

  private applyLobby(lobby: LiveLobbyDto): void {
    const prevId = this.lobby()?.currentQuestion?.sessionQuestionId;
    this.lobby.set(lobby);
    if (lobby.currentQuestion?.sessionQuestionId !== prevId) {
      this.selectedOptionId.set(null);
      this.submitted.set(false);
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

  private connectHub(): void {
    this.hub = this.api.buildHub(false);
    this.hub.on('LobbyUpdated', (payload: LiveLobbyDto) => this.applyLobby(payload));
    this.hub.on('QuestionStarted', (payload: LiveQuestionPayloadDto) => {
      const current = this.lobby();
      if (current) {
        this.applyLobby({
          ...current,
          status: 'Running',
          currentQuestion: payload,
          currentQuestionIndex: payload.index
        });
      }
      this.selectedOptionId.set(null);
      this.submitted.set(false);
      this.syncTimer(payload);
    });
    this.hub.on('QuestionClosed', () => this.submitted.set(true));
    this.hub.on('RevealUpdated', (payload: LiveQuestionPayloadDto) => {
      const current = this.lobby();
      if (current) {
        this.applyLobby({ ...current, currentQuestion: payload, revealCorrect: true });
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
