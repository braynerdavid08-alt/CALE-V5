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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgStyle } from '@angular/common';
import { HubConnection } from '@microsoft/signalr';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { mapApiError } from '../../../core/http/map-api-error';
import { LiveApi } from '../../live/api/live.api';
import { PresentationApi } from './presentation.api';
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
  dtoToEditorSlides,
  findAutoOpenQuestion,
  hasImageCrop,
  imageElementStyles,
  questionToLivePayload
} from './presentation.models';

@Component({
  selector: 'app-presentation-present-page',
  standalone: true,
  imports: [RouterLink, NgStyle],
  templateUrl: './presentation-present.page.html',
  styleUrl: './presentation-present.page.css'
})
export class PresentationPresentPage implements OnInit, OnDestroy {
  @ViewChild('stage') stage?: ElementRef<HTMLElement>;

  private readonly api = inject(PresentationApi);
  private readonly liveApi = inject(LiveApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private hub: HubConnection | null = null;
  private readonly firedQuestionIds = new Set<string>();
  private openingQuestion = false;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly title = signal('');
  readonly slides = signal<EditorSlide[]>([]);
  readonly index = signal(0);
  readonly showChrome = signal(true);
  readonly showNotes = signal(true);
  readonly brokenMediaIds = signal<Set<string>>(new Set());
  readonly presentationId = signal(0);
  readonly slideScale = signal(1);
  readonly liveSessionId = signal<number | null>(null);
  readonly embedMode = signal(false);

  readonly current = computed(() => this.slides()[this.index()] ?? null);
  readonly currentNotes = computed(() => this.current()?.notes?.trim() ?? '');
  readonly showQuestionKey = computed(
    () => !this.embedMode() && this.liveSessionId() == null
  );

  private hideTimer?: ReturnType<typeof setTimeout>;
  private embedRo: ResizeObserver | null = null;
  private pendingEmbedSlide: number | null = null;
  readonly media = resolveMediaUrl;

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.presentationId.set(id);
    this.embedMode.set(this.route.snapshot.queryParamMap.get('embed') === '1');
    const slide = Number(this.route.snapshot.queryParamMap.get('slide') ?? 0);
    const liveId = Number(this.route.snapshot.queryParamMap.get('liveSessionId') ?? 0);
    if (!Number.isNaN(slide) && slide >= 0) {
      this.index.set(slide);
    }
    if (liveId > 0) {
      this.liveSessionId.set(liveId);
    }
    if (this.embedMode()) {
      this.showChrome.set(false);
      this.showNotes.set(false);
      window.addEventListener('message', this.onEmbedMessage);
    }
    this.api.get(id).subscribe({
      next: (detail) => {
        this.title.set(detail.title);
        this.slides.set(dtoToEditorSlides(detail));
        this.loading.set(false);
        this.applyPendingEmbedSlide();
        if (this.embedMode()) {
          window.parent?.postMessage({ type: 'cale-presentation-ready' }, window.location.origin);
        }
        setTimeout(() => {
          this.attachEmbedObserver();
          this.updateSlideScale();
          if (!this.embedMode()) {
            this.enterFullscreen();
          }
          if (this.liveSessionId()) {
            this.connectLiveHub();
            this.onSlideSettled();
          }
        }, 200);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  ngOnDestroy(): void {
    window.removeEventListener('message', this.onEmbedMessage);
    this.embedRo?.disconnect();
    this.embedRo = null;
    if (this.hideTimer) {
      clearTimeout(this.hideTimer);
    }
    void this.hub?.stop();
    if (!this.embedMode() && document.fullscreenElement) {
      void document.exitFullscreen();
    }
  }

  @HostListener('window:resize')
  onResize(): void {
    this.updateSlideScale();
  }

  @HostListener('document:fullscreenchange')
  onFullscreenChange(): void {
    setTimeout(() => this.updateSlideScale(), 50);
  }

  @HostListener('window:keydown', ['$event'])
  onKey(ev: KeyboardEvent): void {
    if (this.embedMode()) {
      return;
    }
    if (ev.key === 'ArrowRight' || ev.key === ' ' || ev.key === 'PageDown') {
      ev.preventDefault();
      this.next();
    } else if (ev.key === 'ArrowLeft' || ev.key === 'PageUp') {
      ev.preventDefault();
      this.prev();
    } else if (ev.key === 'Escape') {
      // First Esc only leaves browser fullscreen; second Esc exits present mode.
      if (document.fullscreenElement) {
        ev.preventDefault();
        void document.exitFullscreen();
        return;
      }
      this.exit();
    } else if (ev.key.toLowerCase() === 'f') {
      this.enterFullscreen();
    } else if (ev.key.toLowerCase() === 'n') {
      ev.preventDefault();
      this.showNotes.update((v) => !v);
    }
    this.bumpChrome();
  }

  next(): void {
    const max = this.slides().length - 1;
    if (this.index() < max) {
      this.index.update((i) => i + 1);
      this.onSlideSettled();
    }
  }

  prev(): void {
    if (this.index() > 0) {
      this.index.update((i) => Math.max(0, i - 1));
      this.onSlideSettled();
    }
  }

  toggleNotes(ev?: Event): void {
    ev?.stopPropagation();
    this.showNotes.update((v) => !v);
    this.bumpChrome();
  }

  bumpChrome(): void {
    if (this.embedMode()) {
      return;
    }
    this.showChrome.set(true);
    if (this.hideTimer) {
      clearTimeout(this.hideTimer);
    }
    this.hideTimer = setTimeout(() => this.showChrome.set(false), 2500);
    this.updateSlideScale();
  }

  onStageClick(): void {
    if (this.embedMode()) {
      return;
    }
    this.next();
    this.bumpChrome();
  }

  updateSlideScale(): void {
    const stage = this.stage?.nativeElement;
    const w = this.embedMode()
      ? stage?.clientWidth || stage?.parentElement?.clientWidth || window.innerWidth
      : window.innerWidth;
    const h = this.embedMode()
      ? stage?.clientHeight || stage?.parentElement?.clientHeight || window.innerHeight
      : window.innerHeight;
    if (w <= 0 || h <= 0) {
      return;
    }

    // Embed: fit whole slide in the iframe. Clicker: cover the projector screen.
    const scale = this.embedMode()
      ? Math.min(w / SLIDE_W, h / SLIDE_H)
      : Math.max(w / SLIDE_W, h / SLIDE_H);
    this.slideScale.set(scale);
  }

  private attachEmbedObserver(): void {
    if (!this.embedMode() || typeof ResizeObserver === 'undefined') {
      return;
    }
    this.embedRo?.disconnect();
    const el = this.stage?.nativeElement;
    if (!el) {
      return;
    }
    this.embedRo = new ResizeObserver(() => this.updateSlideScale());
    this.embedRo.observe(el);
  }

  slidePresentStyle(slide: EditorSlide): Record<string, string> {
    return {
      ...this.bgStyle(slide),
      transform: `scale(${this.slideScale()})`
    };
  }

  enterFullscreen(): void {
    if (this.embedMode()) {
      return;
    }
    const el = this.stage?.nativeElement ?? document.documentElement;
    if (!document.fullscreenElement) {
      void el.requestFullscreen?.().catch(() => {
        /* browsers may block without a fresh user gesture */
      });
    }
  }

  exit(): void {
    if (this.embedMode()) {
      return;
    }
    if (document.fullscreenElement) {
      void document.exitFullscreen();
    }
    const liveId = this.liveSessionId();
    if (liveId) {
      void this.router.navigate(['/teacher/live', liveId, 'host']);
      return;
    }
    void this.router.navigate(['/teacher/presentations', this.presentationId(), 'edit']);
  }

  private readonly onEmbedMessage = (ev: MessageEvent): void => {
    if (ev.origin !== window.location.origin) {
      return;
    }
    if (ev.data?.type !== 'cale-presentation-slide') {
      return;
    }
    const idx = Number(ev.data.slideIndex);
    if (Number.isNaN(idx) || idx < 0) {
      return;
    }
    if (!this.slides().length || idx >= this.slides().length) {
      this.pendingEmbedSlide = idx;
      return;
    }
    this.index.set(idx);
    this.updateSlideScale();
  };

  private applyPendingEmbedSlide(): void {
    const idx = this.pendingEmbedSlide;
    if (idx == null) {
      return;
    }
    if (idx >= 0 && idx < this.slides().length) {
      this.index.set(idx);
      this.pendingEmbedSlide = null;
      this.updateSlideScale();
    }
  }

  private connectLiveHub(): void {
    const sessionId = this.liveSessionId();
    if (!sessionId || this.hub) {
      return;
    }
    this.loadFiredQuestions(sessionId);
    this.hub = this.liveApi.buildHub(true);
    this.hub.on('PresentationSlideChanged', (payload: { slideIndex?: number }) => {
      if (this.embedMode()) {
        return;
      }
      const idx = Number(payload?.slideIndex);
      if (Number.isNaN(idx) || idx < 0 || idx >= this.slides().length) {
        return;
      }
      if (idx === this.index()) {
        return;
      }
      this.index.set(idx);
      this.updateSlideScale();
      // Host already fired the question; only follow the slide.
      this.loadFiredQuestions(sessionId);
    });
    void this.hub.start().then(() => this.hub!.invoke('JoinAsHost', sessionId)).catch(() => {
      this.error.set('No se pudo conectar la sala en vivo. Revisa que la sesión siga abierta.');
    });
  }

  private onSlideSettled(): void {
    void this.syncLiveSlide();
    // Solo el clicker a pantalla completa dispara; el iframe embed lo hace el host.
    if (!this.embedMode()) {
      this.maybeOpenSlideQuestion();
    }
  }

  private async syncLiveSlide(): Promise<void> {
    const sessionId = this.liveSessionId();
    if (!sessionId || !this.hub || this.embedMode()) {
      return;
    }
    try {
      await this.hub.invoke('SyncPresentationSlide', sessionId, this.index());
    } catch {
      /* non-fatal */
    }
  }

  private maybeOpenSlideQuestion(): void {
    const sessionId = this.liveSessionId();
    if (!sessionId || this.openingQuestion) {
      return;
    }
    this.loadFiredQuestions(sessionId);
    const el = findAutoOpenQuestion(this.current());
    if (!el || this.firedQuestionIds.has(el.id)) {
      return;
    }
    const payload = questionToLivePayload(el);
    if (!payload) {
      return;
    }
    this.openingQuestion = true;
    this.firedQuestionIds.add(el.id);
    this.persistFiredQuestions(sessionId);
    this.liveApi.control(sessionId, 'quick', payload).subscribe({
      next: () => {
        this.openingQuestion = false;
      },
      error: () => {
        this.openingQuestion = false;
        this.firedQuestionIds.delete(el.id);
        this.persistFiredQuestions(sessionId);
      }
    });
  }

  private loadFiredQuestions(sessionId: number): void {
    try {
      const raw = localStorage.getItem(`cale-live-q-fired-${sessionId}`);
      const ids = raw ? (JSON.parse(raw) as string[]) : [];
      for (const id of ids) {
        this.firedQuestionIds.add(id);
      }
    } catch {
      /* ignore */
    }
  }

  private persistFiredQuestions(sessionId: number): void {
    try {
      localStorage.setItem(
        `cale-live-q-fired-${sessionId}`,
        JSON.stringify([...this.firedQuestionIds])
      );
    } catch {
      /* ignore */
    }
  }

  bgStyle(slide: EditorSlide): Record<string, string> {
    const css = backgroundCss(slide.background);
    if (css['backgroundImage'] && slide.background.imageUrl) {
      return {
        ...css,
        backgroundImage: `url(${resolveMediaUrl(slide.background.imageUrl)})`
      };
    }
    return css;
  }

  textProps(el: SlideElement): TextProps {
    return el.props as TextProps;
  }

  imageProps(el: SlideElement): ImageProps {
    return el.props as ImageProps;
  }

  imageStyles(el: SlideElement): Record<string, string> {
    if (el.type !== 'image') {
      return {};
    }
    return imageElementStyles(el.props as ImageProps);
  }

  imageHasCrop(el: SlideElement): boolean {
    return el.type === 'image' && hasImageCrop((el.props as ImageProps).crop);
  }

  onMediaError(elementId: string): void {
    this.brokenMediaIds.update((ids) => {
      const next = new Set(ids);
      next.add(elementId);
      return next;
    });
  }

  isMediaBroken(elementId: string): boolean {
    return this.brokenMediaIds().has(elementId);
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

  optionLetter(index: number): string {
    return String.fromCharCode(65 + index);
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
}
