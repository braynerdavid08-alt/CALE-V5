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
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { mapApiError } from '../../../core/http/map-api-error';
import { PresentationApi } from './presentation.api';
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
  dtoToEditorSlides,
  hasImageCrop,
  imageElementStyles
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
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

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

  readonly current = computed(() => this.slides()[this.index()] ?? null);
  readonly currentNotes = computed(() => this.current()?.notes?.trim() ?? '');

  private hideTimer?: ReturnType<typeof setTimeout>;
  readonly media = resolveMediaUrl;

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.presentationId.set(id);
    this.api.get(id).subscribe({
      next: (detail) => {
        this.title.set(detail.title);
        this.slides.set(dtoToEditorSlides(detail));
        this.loading.set(false);
        setTimeout(() => {
          this.updateSlideScale();
          this.enterFullscreen();
        }, 200);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  ngOnDestroy(): void {
    if (this.hideTimer) {
      clearTimeout(this.hideTimer);
    }
    if (document.fullscreenElement) {
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
    if (ev.key === 'ArrowRight' || ev.key === ' ' || ev.key === 'PageDown') {
      ev.preventDefault();
      this.next();
    } else if (ev.key === 'ArrowLeft' || ev.key === 'PageUp') {
      ev.preventDefault();
      this.prev();
    } else if (ev.key === 'Escape') {
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
    if (this.index() < this.slides().length - 1) {
      this.index.update((i) => i + 1);
    }
  }

  prev(): void {
    if (this.index() > 0) {
      this.index.update((i) => i - 1);
    }
  }

  toggleNotes(ev?: Event): void {
    ev?.stopPropagation();
    this.showNotes.update((v) => !v);
    this.bumpChrome();
  }

  onStageClick(): void {
    this.next();
    this.bumpChrome();
  }

  bumpChrome(): void {
    this.showChrome.set(true);
    if (this.hideTimer) {
      clearTimeout(this.hideTimer);
    }
    this.hideTimer = setTimeout(() => this.showChrome.set(false), 2500);
    this.updateSlideScale();
  }

  updateSlideScale(): void {
    const w = window.innerWidth;
    const h = window.innerHeight;
    if (w <= 0 || h <= 0) {
      return;
    }

    // Cover: fill the viewport (may crop edges on non-16:9 screens).
    this.slideScale.set(Math.max(w / SLIDE_W, h / SLIDE_H));
  }

  slidePresentStyle(slide: EditorSlide): Record<string, string> {
    return {
      ...this.bgStyle(slide),
      transform: `scale(${this.slideScale()})`
    };
  }

  enterFullscreen(): void {
    const el = this.stage?.nativeElement ?? document.documentElement;
    if (!document.fullscreenElement) {
      void el.requestFullscreen?.();
    }
  }

  exit(): void {
    if (document.fullscreenElement) {
      void document.exitFullscreen();
    }
    void this.router.navigate(['/teacher/presentations', this.presentationId(), 'edit']);
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
