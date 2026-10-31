import { Component, HostListener, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  LiveAnswerRosterDto,
  LiveApi,
  LiveLobbyDto,
  LiveQuestionPayloadDto,
  sanitizeLiveLobby
} from '../../live/api/live.api';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';

/**
 * Phone remote for the host: advance questions while the PC projects.
 * Open via QR /teacher/live/remote/CXXXXX or by typing the control code.
 */
@Component({
  selector: 'app-teacher-live-remote-page',
  standalone: true,
  imports: [FormsModule, RouterLink, UiButtonComponent, UiErrorComponent],
  templateUrl: './teacher-live-remote.page.html',
  styleUrl: './teacher-live-remote.page.css'
})
export class TeacherLiveRemotePage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(LiveApi);
  private hub: HubConnection | null = null;

  readonly loading = signal(false);
  readonly bootLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly lobby = signal<LiveLobbyDto | null>(null);
  readonly hostAnswerRoster = signal<LiveAnswerRosterDto | null>(null);
  readonly entryCode = signal('');
  readonly needsCode = signal(false);
  private sessionId = 0;

  readonly liveRoster = computed((): LiveAnswerRosterDto | null => {
    const l = this.lobby();
    const q = l?.currentQuestion;
    if (!l || !q || l.status === 'Lobby' || l.status === 'Ended') {
      return null;
    }
    const host = this.hostAnswerRoster();
    if (host && host.sessionQuestionId === q.sessionQuestionId) {
      return host;
    }
    const unanswered = (l.participants ?? []).map((p) => ({
      participantId: p.id,
      displayName: p.displayName,
      answered: false,
      isCorrect: null as boolean | null,
      optionId: null as number | null
    }));
    return {
      sessionQuestionId: q.sessionQuestionId,
      questionIndex: q.index,
      revealCorrectness: true,
      correctCount: 0,
      incorrectCount: 0,
      unansweredCount: unanswered.length,
      correct: [],
      incorrect: [],
      unanswered,
      optionTallies: null
    };
  });

  ngOnInit(): void {
    document.body.classList.add('cale-live-remote');
    document.body.style.overflow = 'hidden';
    const code = (this.route.snapshot.paramMap.get('code') ?? '').trim().toUpperCase();
    const sessionParam = Number(this.route.snapshot.paramMap.get('sessionId') ?? 0);
    if (sessionParam > 0) {
      this.openSession(sessionParam);
      return;
    }
    if (code) {
      this.resolveAndOpen(code);
      return;
    }
    this.needsCode.set(true);
    this.bootLoading.set(false);
  }

  ngOnDestroy(): void {
    document.body.classList.remove('cale-live-remote');
    document.body.style.overflow = '';
    void this.hub?.stop();
  }

  @HostListener('window:keydown', ['$event'])
  onKey(ev: KeyboardEvent): void {
    const key = ev.key;
    if (key !== 'ArrowRight' && key !== ' ' && key !== 'Enter' && key !== 'n' && key !== 'N') {
      return;
    }
    const t = ev.target as HTMLElement | null;
    if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA')) {
      return;
    }
    ev.preventDefault();
    this.advance();
  }

  submitCode(): void {
    const code = this.entryCode().trim().toUpperCase();
    if (code.length < 4) {
      this.error.set('Escribe el código de control (empieza con C).');
      return;
    }
    this.resolveAndOpen(code);
    void this.router.navigate(['/teacher/live/remote', code], { replaceUrl: true });
  }

  advance(): void {
    const l = this.lobby();
    if (!l || l.status === 'Ended' || this.loading()) {
      return;
    }
    if (l.status === 'Lobby') {
      this.control('start');
      return;
    }
    if (l.currentQuestion) {
      this.control('next');
    }
  }

  pauseOrResume(): void {
    const l = this.lobby();
    if (!l) {
      return;
    }
    if (l.status === 'Paused') {
      this.control('resume');
    } else if (l.status === 'Running') {
      this.control('pause');
    }
  }

  endClass(): void {
    if (!confirm('¿Terminar la clase para todos?')) {
      return;
    }
    this.control('end');
  }

  optionLetter(optionId: number | null | undefined): string | null {
    const opts = this.lobby()?.currentQuestion?.options;
    if (!opts || optionId == null) {
      return null;
    }
    const i = opts.findIndex((o) => o.id === optionId);
    return i >= 0 ? String.fromCharCode(65 + i) : null;
  }

  private resolveAndOpen(code: string): void {
    this.bootLoading.set(true);
    this.error.set(null);
    this.needsCode.set(false);
    this.api.resolveRemote(code).subscribe({
      next: (res) => this.openSession(res.sessionId),
      error: (err) => {
        this.bootLoading.set(false);
        this.needsCode.set(true);
        this.error.set(mapApiError(err));
      }
    });
  }

  private openSession(sessionId: number): void {
    this.sessionId = sessionId;
    this.api.getHost(sessionId).subscribe({
      next: (lobby) => {
        this.applyLobby(lobby);
        this.bootLoading.set(false);
        this.connectHub(sessionId);
      },
      error: (err) => {
        this.bootLoading.set(false);
        this.error.set(mapApiError(err));
        this.needsCode.set(true);
      }
    });
  }

  private control(action: string): void {
    if (!this.sessionId) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.api.control(this.sessionId, action).subscribe({
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

  private applyLobby(lobby: LiveLobbyDto): void {
    this.lobby.set(sanitizeLiveLobby(lobby));
  }

  private connectHub(sessionId: number): void {
    void this.hub?.stop();
    this.hub = this.api.buildHub(true);
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
          questionCount: payload.total,
          answersReceived: 0
        });
      }
      this.hostAnswerRoster.set(null);
    });
    this.hub.on('HostAnswerRosterUpdated', (payload: LiveAnswerRosterDto) => {
      this.hostAnswerRoster.set(payload ?? null);
    });
    this.hub.on('AnswerReceived', (payload: { answersReceived?: number }) => {
      const current = this.lobby();
      if (current && typeof payload?.answersReceived === 'number') {
        this.lobby.set({ ...current, answersReceived: payload.answersReceived });
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
}
