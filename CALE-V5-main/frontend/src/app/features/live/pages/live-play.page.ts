import {
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal
} from '@angular/core';
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
  LiveAnswerRosterDto,
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
  QuestionProps,
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
  private stageEl: HTMLElement | null = null;
  private stageRo: ResizeObserver | null = null;
  private deckRootEl: HTMLElement | null = null;
  private chromeHideTimer: ReturnType<typeof setTimeout> | null = null;

  @ViewChild('deckRoot')
  set deckRootRef(ref: ElementRef<HTMLElement> | undefined) {
    this.deckRootEl = ref?.nativeElement ?? null;
  }

  @ViewChild('deckStage')
  set deckStageRef(ref: ElementRef<HTMLElement> | undefined) {
    this.detachStageObserver();
    this.stageEl = ref?.nativeElement ?? null;
    if (!this.stageEl || typeof ResizeObserver === 'undefined') {
      return;
    }
    this.stageRo = new ResizeObserver(() => this.fitDeck());
    this.stageRo.observe(this.stageEl);
    this.fitDeck();
  }

  readonly lobby = signal<LiveLobbyDto | null>(null);
  readonly ranking = signal<LiveRankingDto | null>(null);
  readonly doubts = signal<LiveDoubtDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly connectionNotice = signal<string | null>(null);
  readonly secondsLeft = signal<number | null>(null);
  readonly selectedOptionId = signal<number | null>(null);
  readonly submitted = signal(false);
  readonly displayName = signal('');
  readonly doubtText = signal('');
  readonly lastPoints = signal<number | null>(null);
  readonly rematchCode = signal<string | null>(null);
  readonly answerRoster = signal<LiveAnswerRosterDto | null>(null);

  readonly presentationSlides = signal<EditorSlide[]>([]);
  readonly presentationTitle = signal('');
  readonly presentationSlide = signal(0);
  readonly presentationLoaded = signal(false);
  readonly deckScale = signal(0.2);
  readonly deckOffsetX = signal(0);
  readonly deckOffsetY = signal(0);
  readonly deckFullscreen = signal(false);
  readonly deckChromeVisible = signal(true);

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
    if (this.chromeHideTimer) {
      clearTimeout(this.chromeHideTimer);
    }
    this.setDeckFullscreen(false);
    this.detachStageObserver();
    void this.hub?.stop();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.deckFullscreen()) {
      this.setDeckFullscreen(false);
    }
  }

  @HostListener('document:fullscreenchange')
  @HostListener('document:webkitfullscreenchange')
  onFsChange(): void {
    const active = !!(document.fullscreenElement || (document as Document & { webkitFullscreenElement?: Element }).webkitFullscreenElement);
    if (!active && this.deckFullscreen()) {
      this.deckFullscreen.set(false);
      document.body.style.overflow = '';
      this.clearChromeHideTimer();
      requestAnimationFrame(() => this.fitDeck());
    } else if (active) {
      requestAnimationFrame(() => this.fitDeck());
    }
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
    return true;
  }

  presentationCompact(): boolean {
    if (this.deckFullscreen()) {
      return false;
    }
    const l = this.lobby();
    return !!(l?.currentQuestion && l.status !== 'Lobby' && l.status !== 'Paused');
  }

  toggleDeckFullscreen(): void {
    this.setDeckFullscreen(!this.deckFullscreen());
  }

  bumpDeckChrome(ev?: Event): void {
    if (!this.deckFullscreen()) {
      return;
    }
    ev?.stopPropagation();
    this.deckChromeVisible.set(true);
    this.scheduleChromeHide();
  }

  onFullscreenStageTap(): void {
    if (!this.deckFullscreen()) {
      return;
    }
    this.deckChromeVisible.update((v) => !v);
    if (this.deckChromeVisible()) {
      this.scheduleChromeHide();
    } else {
      this.clearChromeHideTimer();
    }
  }

  setDeckFullscreen(on: boolean): void {
    this.deckFullscreen.set(on);
    document.body.style.overflow = on ? 'hidden' : '';
    this.clearChromeHideTimer();
    if (on) {
      this.deckChromeVisible.set(true);
      this.scheduleChromeHide();
      void this.enterBrowserFullscreen();
    } else {
      this.deckChromeVisible.set(true);
      void this.exitBrowserFullscreen();
    }
    requestAnimationFrame(() => this.fitDeck());
  }

  private scheduleChromeHide(): void {
    this.clearChromeHideTimer();
    this.chromeHideTimer = setTimeout(() => {
      if (this.deckFullscreen()) {
        this.deckChromeVisible.set(false);
      }
    }, 2200);
  }

  private clearChromeHideTimer(): void {
    if (this.chromeHideTimer) {
      clearTimeout(this.chromeHideTimer);
      this.chromeHideTimer = null;
    }
  }

  private async enterBrowserFullscreen(): Promise<void> {
    const el = this.deckRootEl as HTMLElement & {
      webkitRequestFullscreen?: () => Promise<void> | void;
    };
    if (!el) {
      return;
    }
    try {
      if (!document.fullscreenElement && el.requestFullscreen) {
        await el.requestFullscreen();
      } else if (el.webkitRequestFullscreen) {
        await el.webkitRequestFullscreen();
      }
    } catch {
      /* iOS Safari often blocks; CSS fullscreen still works */
    }
  }

  private async exitBrowserFullscreen(): Promise<void> {
    const doc = document as Document & { webkitExitFullscreen?: () => Promise<void> | void };
    try {
      if (document.fullscreenElement) {
        await document.exitFullscreen();
      } else if (doc.webkitExitFullscreen) {
        await doc.webkitExitFullscreen();
      }
    } catch {
      /* ignore */
    }
  }

  private detachStageObserver(): void {
    this.stageRo?.disconnect();
    this.stageRo = null;
  }

  private fitDeck(): void {
    const el = this.stageEl;
    if (!el) {
      return;
    }
    const w = el.clientWidth;
    const h = el.clientHeight;
    if (w < 8 || h < 8) {
      return;
    }
    // Always contain the full slide (no crop). Cleaner on phones; black bars only if needed.
    const scale = Math.max(0.05, Math.min(w / SLIDE_W, h / SLIDE_H));
    this.deckScale.set(scale);
    this.deckOffsetX.set(Math.round((w - SLIDE_W * scale) / 2));
    this.deckOffsetY.set(Math.round((h - SLIDE_H * scale) / 2));
  }

  optionLetter(index: number): string {
    return String.fromCharCode(65 + index);
  }

  isWrongSelected(optId: number): boolean {
    const l = this.lobby();
    const q = l?.currentQuestion;
    if (!l?.revealCorrect || !q || this.selectedOptionId() !== optId) {
      return false;
    }
    const opt = q.options.find((o) => o.id === optId);
    return opt?.isCorrect === false;
  }

  myRosterResult(): 'correct' | 'incorrect' | 'unanswered' | null {
    const roster = this.answerRoster();
    const q = this.lobby()?.currentQuestion;
    if (!roster || !q || roster.sessionQuestionId !== q.sessionQuestionId || !roster.revealCorrectness) {
      return null;
    }
    const id =
      readLiveParticipant(this.sessionId)?.participantId
      ?? this.ranking()?.myParticipantId
      ?? null;
    if (id == null) {
      // Fallback: infer from selected option vs reveal
      const selected = this.selectedOptionId();
      if (selected == null) {
        return this.submitted() ? 'unanswered' : null;
      }
      const opt = q.options.find((o) => o.id === selected);
      if (opt?.isCorrect === true) {
        return 'correct';
      }
      if (opt?.isCorrect === false) {
        return 'incorrect';
      }
      return null;
    }
    if (roster.correct.some((r) => r.participantId === id)) {
      return 'correct';
    }
    if (roster.incorrect.some((r) => r.participantId === id)) {
      return 'incorrect';
    }
    if (roster.unanswered.some((r) => r.participantId === id)) {
      return 'unanswered';
    }
    return null;
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

  questionProps(el: SlideElement): QuestionProps {
    return el.props as QuestionProps;
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
    requestAnimationFrame(() => {
      this.fitDeck();
      this.tryPlaySlideVideos();
    });
  }

  private setPresentationSlide(index: number): void {
    const max = Math.max(0, this.presentationSlides().length - 1);
    this.presentationSlide.set(Math.max(0, Math.min(index, max)));
    requestAnimationFrame(() => this.tryPlaySlideVideos());
  }

  tryPlaySlideVideos(): void {
    const root = this.stageEl;
    if (!root) {
      return;
    }
    const videos = root.querySelectorAll('video');
    videos.forEach((node) => {
      const video = node as HTMLVideoElement;
      video.muted = true;
      video.setAttribute('playsinline', '');
      video.setAttribute('webkit-playsinline', '');
      const play = video.play();
      if (play && typeof play.catch === 'function') {
        play.catch(() => {
          /* controls remain for manual play */
        });
      }
    });
  }

  onSlideVideoError(_ev: Event): void {
    /* keep controls so student can retry */
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
    this.hub.onreconnecting(() => {
      this.connectionNotice.set('Reconectando… espera un momento.');
    });
    this.hub.onreconnected(() => {
      this.connectionNotice.set('Conexión restablecida.');
      void this.hub!.invoke('JoinAsParticipant', this.sessionId, this.token).finally(() => {
        window.setTimeout(() => {
          if (this.connectionNotice() === 'Conexión restablecida.') {
            this.connectionNotice.set(null);
          }
        }, 2500);
      });
    });
    this.hub.onclose(() => {
      this.connectionNotice.set('Sin conexión. Revisa tu red.');
    });
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
      this.answerRoster.set(null);
      this.syncTimer(payload);
    });
    this.hub.on('QuestionClosed', () => this.submitted.set(true));
    this.hub.on('RevealUpdated', (payload: LiveQuestionPayloadDto) => {
      const current = this.lobby();
      if (current) {
        this.applyLobby({ ...current, currentQuestion: payload, revealCorrect: true });
      }
    });
    this.hub.on('AnswerRosterUpdated', (payload: LiveAnswerRosterDto) => {
      this.answerRoster.set(payload ?? null);
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
