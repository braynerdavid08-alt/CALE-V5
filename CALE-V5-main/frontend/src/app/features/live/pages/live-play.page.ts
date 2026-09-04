import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgStyle } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import {
  LiveApi,
  LiveDoubtDto,
  LiveLobbyDto,
  LivePresentationDto,
  LiveQuestionPayloadDto,
  LiveRankingDto,
  sanitizeLiveLobby
} from '../api/live.api';
import { computeSecondsLeft } from '../live-timer.util';
import { readLiveParticipant, saveLiveParticipant } from './live-join.page';
import {
  EditorSlide,
  ImageProps,
  LineProps,
  SLIDE_H,
  SLIDE_W,
  ShapeKind,
  ShapeProps,
  SlideElement,
  TextProps,
  VideoProps,
  backgroundCss,
  hasImageCrop,
  imageElementStyles,
  newClientId,
  parseBackground,
  parseElements,
  unlockImportedPhotoSlide
} from '../../teacher/presentations/presentation.models';

@Component({
  selector: 'app-live-play-page',
  standalone: true,
  imports: [FormsModule, RouterLink, UiButtonComponent, UiErrorComponent, NgStyle],
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

  readonly presentationSlides = signal<EditorSlide[]>([]);
  readonly presentationTitle = signal('');
  readonly presentationSlide = signal(0);
  readonly presentationLoaded = signal(false);

  readonly currentPresentationSlide = computed(
    () => this.presentationSlides()[this.presentationSlide()] ?? null
  );

  readonly media = resolveMediaUrl;
  readonly slideW = SLIDE_W;
  readonly slideH = SLIDE_H;

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
      this.presentationSlides.set([]);
      this.presentationLoaded.set(false);
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

  hasLinkedPresentation(): boolean {
    return !!(this.lobby()?.config?.presentationId && this.presentationLoaded());
  }

  showPresentationPanel(): boolean {
    const l = this.lobby();
    if (!l || !this.hasLinkedPresentation() || l.status === 'Ended') {
      return false;
    }
    // Show slides while waiting or between questions; hide during active question to focus answers.
    return !l.currentQuestion || l.status === 'Lobby' || l.status === 'Paused';
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

  bgStyle(slide: EditorSlide): Record<string, string> {
    return {
      width: `${SLIDE_W}px`,
      height: `${SLIDE_H}px`,
      ...backgroundCss(slide.background)
    };
  }

  textProps(el: SlideElement): TextProps {
    return el.props as TextProps;
  }

  imageProps(el: SlideElement): ImageProps {
    return el.props as ImageProps;
  }

  videoProps(el: SlideElement): VideoProps {
    return el.props as VideoProps;
  }

  shapeProps(el: SlideElement): ShapeProps {
    return el.props as ShapeProps;
  }

  lineProps(el: SlideElement): LineProps {
    return el.props as LineProps;
  }

  imageHasCrop(el: SlideElement): boolean {
    return el.type === 'image' && hasImageCrop((el.props as ImageProps).crop);
  }

  imageStyles(el: SlideElement): Record<string, string> {
    if (el.type !== 'image') {
      return {};
    }
    return imageElementStyles(el.props as ImageProps);
  }

  shapeClip(shape: ShapeKind): string | null {
    if (shape === 'triangle') {
      return 'polygon(50% 0%, 0% 100%, 100% 100%)';
    }
    if (shape === 'octagon') {
      return 'polygon(30% 0%, 70% 0%, 100% 30%, 100% 70%, 70% 100%, 30% 100%, 0% 70%, 0% 30%)';
    }
    return null;
  }

  private reload(): void {
    this.api.getPlay(this.sessionId, this.token).subscribe({
      next: (lobby) => {
        this.applyLobby(lobby);
        this.loadPresentationIfNeeded(lobby);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private loadPresentationIfNeeded(lobby: LiveLobbyDto): void {
    const presentationId = lobby.config?.presentationId;
    if (!presentationId) {
      this.presentationLoaded.set(false);
      this.presentationSlides.set([]);
      return;
    }
    if (this.presentationLoaded() && this.presentationSlides().length) {
      return;
    }
    this.api.getPresentation(this.sessionId, this.token).subscribe({
      next: (deck) => this.applyPresentation(deck),
      error: () => {
        this.presentationLoaded.set(false);
        this.presentationSlides.set([]);
      }
    });
  }

  private applyPresentation(deck: LivePresentationDto): void {
    const slides = deck.slides
      .slice()
      .sort((a, b) => a.position - b.position)
      .map((s) =>
        unlockImportedPhotoSlide({
          clientId: newClientId('live-slide'),
          title: s.title,
          notes: '',
          background: parseBackground(s.backgroundJson),
          elements: parseElements(s.elementsJson)
        })
      );
    this.presentationTitle.set(deck.title);
    this.presentationSlides.set(slides);
    this.presentationSlide.set(
      Math.max(0, Math.min(deck.slideIndex, Math.max(0, slides.length - 1)))
    );
    this.presentationLoaded.set(true);
  }

  private setPresentationSlide(index: number): void {
    const max = Math.max(0, this.presentationSlides().length - 1);
    this.presentationSlide.set(Math.max(0, Math.min(index, max)));
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
    this.loadPresentationIfNeeded(lobby);
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
    this.hub.on('PresentationSlideChanged', (payload: { slideIndex?: number }) => {
      if (typeof payload?.slideIndex === 'number') {
        this.setPresentationSlide(payload.slideIndex);
      }
    });
    void this.hub.start().then(() =>
      this.hub!.invoke('JoinAsParticipant', this.sessionId, this.token)
    );
  }
}
