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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DecimalPipe, NgStyle } from '@angular/common';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { mapApiError } from '../../../core/http/map-api-error';
import { BRAND } from '../../../core/brand';
import { PresentationApi } from './presentation.api';
import {
  EditorSlide,
  ImageCrop,
  ImageProps,
  LineProps,
  PRESENTATION_CATEGORIES,
  PRESENTATION_MEDIA_MAX_BYTES,
  SLIDE_H,
  SLIDE_W,
  ShapeKind,
  ShapeProps,
  SlideElement,
  TEMPLATE_OPTIONS,
  TextProps,
  VideoProps,
  backgroundCss,
  dtoToEditorSlides,
  hasImageCrop,
  imageElementStyles,
  newClientId,
  normalizeImageCrop
} from './presentation.models';

type SaveState = 'idle' | 'dirty' | 'saving' | 'saved' | 'offline' | 'error';

@Component({
  selector: 'app-presentation-editor-page',
  standalone: true,
  imports: [FormsModule, RouterLink, NgStyle, DecimalPipe],
  templateUrl: './presentation-editor.page.html',
  styleUrl: './presentation-editor.page.css'
})
export class PresentationEditorPage implements OnInit, OnDestroy {
  @ViewChild('canvasHost') canvasHost?: ElementRef<HTMLElement>;
  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;
  @ViewChild('videoInput') videoInput?: ElementRef<HTMLInputElement>;

  private readonly api = inject(PresentationApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly brand = BRAND;
  readonly slideW = SLIDE_W;
  readonly slideH = SLIDE_H;
  readonly categories = PRESENTATION_CATEGORIES;
  readonly templates = TEMPLATE_OPTIONS;
  readonly media = resolveMediaUrl;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly presentationId = signal(0);
  readonly title = signal('');
  readonly description = signal('');
  readonly category = signal<string>(PRESENTATION_CATEGORIES[0]);
  readonly groupId = signal<number | null>(null);
  readonly slides = signal<EditorSlide[]>([]);
  readonly activeIndex = signal(0);
  readonly selectedId = signal<string | null>(null);
  readonly zoom = signal(1);
  readonly saveState = signal<SaveState>('idle');
  readonly lastSavedAt = signal<number | null>(null);
  readonly editingText = signal(false);
  readonly showImageModal = signal(false);
  readonly imageUrlInput = signal('');
  readonly imageUploading = signal(false);
  readonly imageModalReplace = signal(false);

  readonly activeSlide = computed(() => this.slides()[this.activeIndex()] ?? null);
  readonly selected = computed(() => {
    const slide = this.activeSlide();
    const id = this.selectedId();
    if (!slide || !id) {
      return null;
    }
    return slide.elements.find((e) => e.id === id) ?? null;
  });

  readonly saveLabel = computed(() => {
    const s = this.saveState();
    if (s === 'saving') {
      return 'Guardando…';
    }
    if (s === 'offline') {
      return 'Sin conexión — borrador local';
    }
    if (s === 'error') {
      return 'Error al guardar';
    }
    if (s === 'dirty') {
      return 'Cambios sin guardar';
    }
    const t = this.lastSavedAt();
    if (s === 'saved' && t) {
      const sec = Math.max(0, Math.floor((Date.now() - t) / 1000));
      if (sec < 5) {
        return 'Guardado';
      }
      return `Guardado hace ${sec} s`;
    }
    return 'Guardado';
  });

  private autosaveTimer?: ReturnType<typeof setTimeout>;
  private labelTimer?: ReturnType<typeof setInterval>;
  private drag:
    | {
        id: string;
        mode: 'move' | 'resize';
        resizeDir?: 'se' | 'sw' | 'ne' | 'nw' | 'e' | 'w' | 'n' | 's';
        startX: number;
        startY: number;
        origX: number;
        origY: number;
        origW: number;
        origH: number;
        aspect?: number;
        lockAspect: boolean;
      }
    | null = null;
  private clipboard: SlideElement | null = null;
  private skipUnload = false;

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) {
      void this.router.navigate(['/teacher/presentations']);
      return;
    }
    this.presentationId.set(id);
    this.api.get(id).subscribe({
      next: (detail) => {
        this.title.set(detail.title);
        this.description.set(detail.description || '');
        this.category.set(detail.category || PRESENTATION_CATEGORIES[0]);
        this.groupId.set(detail.groupId ?? null);
        this.slides.set(dtoToEditorSlides(detail));
        this.loading.set(false);
        this.saveState.set('saved');
        this.lastSavedAt.set(Date.now());
        this.tryRestoreLocalDraft(id);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
    this.labelTimer = setInterval(() => {
      if (this.saveState() === 'saved') {
        this.lastSavedAt.update((v) => v);
      }
    }, 1000);
  }

  ngOnDestroy(): void {
    if (this.autosaveTimer) {
      clearTimeout(this.autosaveTimer);
    }
    if (this.labelTimer) {
      clearInterval(this.labelTimer);
    }
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(ev: BeforeUnloadEvent): void {
    if (this.skipUnload || this.saveState() !== 'dirty') {
      return;
    }
    this.persistLocalDraft();
    ev.preventDefault();
    ev.returnValue = '';
  }

  @HostListener('window:keydown', ['$event'])
  onKey(ev: KeyboardEvent): void {
    if (ev.key === 'Escape') {
      if (this.showImageModal()) {
        this.closeImageModal();
        return;
      }
      if (this.editingText()) {
        this.editingText.set(false);
      }
      return;
    }
    if (this.editingText()) {
      return;
    }
    if (ev.key === 'Enter' && this.selectedId()) {
      const sel = this.selected();
      if (sel?.type === 'text') {
        ev.preventDefault();
        this.startEditText(sel.id, ev);
      }
      return;
    }
    const meta = ev.ctrlKey || ev.metaKey;
    if (meta && ev.key.toLowerCase() === 's') {
      ev.preventDefault();
      this.saveNow();
      return;
    }
    if (meta && ev.key.toLowerCase() === 'd') {
      ev.preventDefault();
      this.duplicateElement();
      return;
    }
    if (meta && ev.key.toLowerCase() === 'c') {
      this.copyElement();
      return;
    }
    if (meta && ev.key.toLowerCase() === 'v') {
      ev.preventDefault();
      this.pasteElement();
      return;
    }
    if (ev.key === 'Delete' || ev.key === 'Backspace') {
      if (this.selectedId()) {
        ev.preventDefault();
        this.deleteSelected();
      }
      return;
    }
    if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(ev.key) && this.selectedId()) {
      ev.preventDefault();
      const dx = ev.key === 'ArrowLeft' ? -4 : ev.key === 'ArrowRight' ? 4 : 0;
      const dy = ev.key === 'ArrowUp' ? -4 : ev.key === 'ArrowDown' ? 4 : 0;
      this.nudgeSelected(dx, dy);
    }
  }

  fitZoom(): void {
    const host = this.canvasHost?.nativeElement;
    if (!host) {
      this.zoom.set(0.85);
      return;
    }
    const pad = 48;
    const zx = (host.clientWidth - pad) / SLIDE_W;
    const zy = (host.clientHeight - pad) / SLIDE_H;
    this.zoom.set(Math.max(0.35, Math.min(1.25, Math.min(zx, zy))));
  }

  setZoom(v: number): void {
    this.zoom.set(Math.max(0.35, Math.min(1.5, v)));
  }

  markDirty(): void {
    this.saveState.set('dirty');
    this.persistLocalDraft();
    if (this.autosaveTimer) {
      clearTimeout(this.autosaveTimer);
    }
    this.autosaveTimer = setTimeout(() => this.saveNow(true), 1400);
  }

  saveNow(auto = false): void {
    const id = this.presentationId();
    if (!id || this.saveState() === 'saving') {
      return;
    }
    this.saveState.set('saving');
    const payload = {
      title: this.title().trim() || 'Sin título',
      description: this.description().trim() || null,
      category: this.category(),
      groupId: this.groupId(),
      thumbnailUrl: null as string | null,
      slides: this.slides().map((s) => ({
        id: s.id ?? null,
        title: s.title,
        notes: s.notes || null,
        backgroundJson: JSON.stringify(s.background),
        elementsJson: JSON.stringify(s.elements)
      }))
    };
    this.api.saveDocument(id, payload).subscribe({
      next: (detail) => {
        this.slides.set(dtoToEditorSlides(detail));
        this.saveState.set('saved');
        this.lastSavedAt.set(Date.now());
        this.clearLocalDraft(id);
        if (!auto) {
          this.error.set(null);
        }
      },
      error: (err) => {
        this.persistLocalDraft();
        if (!navigator.onLine) {
          this.saveState.set('offline');
        } else {
          this.saveState.set('error');
          this.error.set(mapApiError(err));
        }
      }
    });
  }

  selectSlide(i: number): void {
    this.activeIndex.set(i);
    this.selectedId.set(null);
    this.editingText.set(false);
  }

  addSlide(templateKey = 'blank'): void {
    const slides = [...this.slides()];
    const blank: EditorSlide = {
      clientId: newClientId('slide'),
      title: `Diapositiva ${slides.length + 1}`,
      notes: '',
      background: { type: 'solid', color: '#F7F9FC' },
      elements: [
        {
          id: newClientId('el'),
          type: 'text',
          x: 80,
          y: 200,
          w: 800,
          h: 80,
          rotation: 0,
          z: 1,
          props: {
            text: 'Nueva diapositiva',
            fontSize: 32,
            fontWeight: 600,
            color: '#0B1F33',
            align: 'center',
            fontFamily: 'Segoe UI, sans-serif'
          }
        }
      ]
    };
    if (templateKey !== 'blank') {
      // Keep simple blank for add; templates applied at create time.
    }
    slides.splice(this.activeIndex() + 1, 0, blank);
    this.slides.set(slides);
    this.activeIndex.set(this.activeIndex() + 1);
    this.markDirty();
  }

  duplicateSlide(): void {
    const cur = this.activeSlide();
    if (!cur) {
      return;
    }
    const copy: EditorSlide = {
      clientId: newClientId('slide'),
      title: `${cur.title} (copia)`,
      notes: cur.notes,
      background: { ...cur.background },
      elements: cur.elements.map((e) => ({
        ...e,
        id: newClientId('el'),
        props: { ...e.props } as SlideElement['props']
      }))
    };
    const slides = [...this.slides()];
    slides.splice(this.activeIndex() + 1, 0, copy);
    this.slides.set(slides);
    this.activeIndex.set(this.activeIndex() + 1);
    this.markDirty();
  }

  deleteSlide(): void {
    if (this.slides().length <= 1) {
      return;
    }
    const slides = [...this.slides()];
    slides.splice(this.activeIndex(), 1);
    this.slides.set(slides);
    this.activeIndex.set(Math.max(0, this.activeIndex() - 1));
    this.selectedId.set(null);
    this.markDirty();
  }

  moveSlide(from: number, to: number): void {
    if (to < 0 || to >= this.slides().length || from === to) {
      return;
    }
    const slides = [...this.slides()];
    const [item] = slides.splice(from, 1);
    slides.splice(to, 0, item);
    this.slides.set(slides);
    this.activeIndex.set(to);
    this.markDirty();
  }

  onSlideDragStart(ev: DragEvent, index: number): void {
    ev.dataTransfer?.setData('text/plain', String(index));
  }

  onSlideDrop(ev: DragEvent, index: number): void {
    ev.preventDefault();
    const from = Number(ev.dataTransfer?.getData('text/plain'));
    if (!Number.isNaN(from)) {
      this.moveSlide(from, index);
    }
  }

  selectElement(id: string, ev?: Event): void {
    ev?.stopPropagation();
    if (this.selectedId() !== id) {
      this.editingText.set(false);
    }
    this.selectedId.set(id);
  }

  clearSelection(): void {
    this.selectedId.set(null);
    this.editingText.set(false);
  }

  onElementPointerDown(ev: PointerEvent, el: SlideElement): void {
    if (this.editingText() && this.selectedId() === el.id && el.type === 'text') {
      return;
    }

    if (el.type === 'text') {
      const inText = (ev.target as HTMLElement).closest('.el-text');
      if (inText) {
        ev.stopPropagation();
        this.startEditText(el.id, ev);
        return;
      }

      if (ev.altKey) {
        this.startDrag(ev, el.id, 'move');
      }
      return;
    }

    this.startDrag(ev, el.id, 'move');
  }

  startEditText(id: string, ev: Event): void {
    ev.stopPropagation();
    const slideEl = this.activeSlide()?.elements.find((e) => e.id === id);
    if (!slideEl || slideEl.type !== 'text') {
      return;
    }
    const initialText = (slideEl.props as TextProps).text;
    this.selectedId.set(id);
    this.editingText.set(true);

    queueMicrotask(() => {
      const node = document.querySelector(`[data-text-id="${id}"]`) as HTMLElement | null;
      if (!node) {
        return;
      }
      node.innerText = initialText;
      node.focus();
      const range = document.createRange();
      const selection = window.getSelection();
      range.selectNodeContents(node);
      range.collapse(false);
      selection?.removeAllRanges();
      selection?.addRange(range);
    });
  }

  finishTextEdit(): void {
    this.editingText.set(false);
    this.markDirty();
  }

  onTextInput(id: string, ev: Event): void {
    const text = (ev.target as HTMLElement).innerText.replace(/\u00a0/g, ' ');
    this.patchElement(id, (el) => {
      if (el.type !== 'text') {
        return el;
      }
      return { ...el, props: { ...(el.props as TextProps), text } };
    });
    this.markDirty();
  }

  addText(kind: 'title' | 'subtitle' | 'body' = 'body'): void {
    const sizes = { title: 40, subtitle: 28, body: 20 };
    const el: SlideElement = {
      id: newClientId('el'),
      type: 'text',
      x: 120,
      y: 160,
      w: 700,
      h: kind === 'body' ? 120 : 70,
      rotation: 0,
      z: this.nextZ(),
      props: {
        text: kind === 'title' ? 'Título' : kind === 'subtitle' ? 'Subtítulo' : 'Texto',
        fontSize: sizes[kind],
        fontWeight: kind === 'body' ? 400 : 700,
        color: '#0B1F33',
        align: 'left',
        fontFamily: 'Segoe UI, sans-serif'
      }
    };
    this.pushElement(el);
    queueMicrotask(() => this.startEditText(el.id, new Event('init')));
  }

  addShape(shape: ShapeKind): void {
    const el: SlideElement = {
      id: newClientId('el'),
      type: 'shape',
      x: 200,
      y: 160,
      w: shape === 'ellipse' ? 180 : 200,
      h: shape === 'ellipse' ? 180 : 140,
      rotation: 0,
      z: this.nextZ(),
      props: {
        shape,
        fill: shape === 'octagon' ? '#D32F2F' : '#2BB0ED',
        stroke: '#0B1F33',
        strokeWidth: 2,
        opacity: 1
      }
    };
    this.pushElement(el);
  }

  addLine(arrow = false): void {
    const el: SlideElement = {
      id: newClientId('el'),
      type: arrow ? 'arrow' : 'line',
      x: 180,
      y: 260,
      w: 320,
      h: 40,
      rotation: 0,
      z: this.nextZ(),
      props: { color: '#0B1F33', strokeWidth: 4, arrowEnd: arrow }
    };
    this.pushElement(el);
  }

  addLogo(): void {
    const el: SlideElement = {
      id: newClientId('el'),
      type: 'text',
      x: 40,
      y: 20,
      w: 280,
      h: 36,
      rotation: 0,
      z: this.nextZ(),
      props: {
        text: 'Mi CALE',
        fontSize: 18,
        fontWeight: 700,
        color: '#2BB0ED',
        align: 'left',
        fontFamily: 'Segoe UI, sans-serif'
      }
    };
    this.pushElement(el);
  }

  triggerImage(): void {
    this.imageModalReplace.set(false);
    this.imageUrlInput.set('');
    this.showImageModal.set(true);
  }

  triggerVideo(): void {
    this.videoInput?.nativeElement.click();
  }

  openReplaceImageModal(): void {
    const sel = this.selected();
    if (!sel || sel.type !== 'image') {
      return;
    }
    this.imageModalReplace.set(true);
    this.imageUrlInput.set(this.imageProps(sel).src);
    this.showImageModal.set(true);
  }

  closeImageModal(): void {
    this.showImageModal.set(false);
    this.imageUrlInput.set('');
    this.imageUploading.set(false);
    this.imageModalReplace.set(false);
  }

  onImageSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    if (file.size > PRESENTATION_MEDIA_MAX_BYTES) {
      this.error.set('El archivo debe pesar 100 MB o menos.');
      return;
    }
    if (this.imageModalReplace()) {
      this.uploadAndApplyToSelected(file);
    } else {
      this.uploadAndInsertMedia(file);
    }
  }

  onVideoSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    if (file.size > PRESENTATION_MEDIA_MAX_BYTES) {
      this.error.set('El video debe pesar 100 MB o menos.');
      return;
    }
    this.uploadAndInsertMedia(file);
  }

  addImageFromUrl(): void {
    const src = this.normalizeImageSrc(this.imageUrlInput());
    if (!src) {
      this.error.set('URL inválida. Usa https://... o /uploads/...');
      return;
    }
    if (this.imageModalReplace()) {
      void this.applyImageSrcToSelected(src);
      this.closeImageModal();
      return;
    }
    void this.insertImageFromSrc(src).then(() => this.closeImageModal());
  }

  onCanvasDrop(ev: DragEvent): void {
    ev.preventDefault();
    const file = ev.dataTransfer?.files?.[0];
    if (!file) {
      return;
    }
    if (file.size > PRESENTATION_MEDIA_MAX_BYTES) {
      this.error.set('El archivo debe pesar 100 MB o menos.');
      return;
    }
    const pos = this.dropPosition(ev);
    this.uploadAndInsertMedia(file, pos.x, pos.y);
  }

  updateImageProp<K extends keyof ImageProps>(key: K, value: ImageProps[K]): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.patchElement(id, (el) => {
      if (el.type !== 'image') {
        return el;
      }
      return { ...el, props: { ...(el.props as ImageProps), [key]: value } };
    });
    this.markDirty();
  }

  updateVideoProp<K extends keyof VideoProps>(key: K, value: VideoProps[K]): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.patchElement(id, (el) => {
      if (el.type !== 'video') {
        return el;
      }
      return { ...el, props: { ...(el.props as VideoProps), [key]: value } };
    });
    this.markDirty();
  }

  updateImageGeometry(key: 'x' | 'y' | 'w' | 'h', raw: number): void {
    const id = this.selectedId();
    if (!id || !Number.isFinite(raw)) {
      return;
    }
    this.patchElement(id, (el) => {
      if (el.type !== 'image') {
        return el;
      }
      if (key === 'w') {
        return { ...el, w: Math.max(24, Math.round(raw)) };
      }
      if (key === 'h') {
        return { ...el, h: Math.max(24, Math.round(raw)) };
      }
      if (key === 'x') {
        return { ...el, x: Math.round(raw) };
      }
      return { ...el, y: Math.round(raw) };
    });
    this.markDirty();
  }

  fitImageToSlide(): void {
    const id = this.selectedId();
    const sel = this.selected();
    if (!id || !sel || sel.type !== 'image') {
      return;
    }
    const props = sel.props as ImageProps;
    void this.probeNaturalSize(props.src).then((natural) => {
      const maxW = SLIDE_W - 32;
      const maxH = SLIDE_H - 32;
      const scale = Math.min(1, maxW / natural.w, maxH / natural.h);
      const w = Math.max(40, Math.round(natural.w * scale));
      const h = Math.max(24, Math.round(natural.h * scale));
      this.patchElement(id, (el) => ({
        ...el,
        x: Math.round((SLIDE_W - w) / 2),
        y: Math.round((SLIDE_H - h) / 2),
        w,
        h
      }));
      this.markDirty();
    });
  }

  setImageObjectFit(fit: 'contain' | 'cover'): void {
    this.updateImageProp('objectFit', fit);
    if (fit === 'cover') {
      this.updateImageProp('crop', undefined);
    }
  }

  updateImageCrop(key: keyof ImageCrop, raw: number): void {
    const id = this.selectedId();
    const sel = this.selected();
    if (!id || !sel || sel.type !== 'image') {
      return;
    }
    if (!Number.isFinite(raw)) {
      return;
    }
    const current = normalizeImageCrop((sel.props as ImageProps).crop);
    const next = normalizeImageCrop({ ...current, [key]: raw / 100 });
    this.updateImageProp('crop', next);
  }

  resetImageCrop(): void {
    this.updateImageProp('crop', undefined);
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

  imageCropPercent(key: keyof ImageCrop): number {
    const sel = this.selected();
    if (!sel || sel.type !== 'image') {
      return key === 'w' || key === 'h' ? 100 : 0;
    }
    const crop = normalizeImageCrop((sel.props as ImageProps).crop);
    return Math.round(crop[key] * 100);
  }

  private uploadAndInsertMedia(file: File, x?: number, y?: number): void {
    this.imageUploading.set(true);
    this.api.upload(file).subscribe({
      next: (res) => {
        this.imageUploading.set(false);
        const isVideo = res.mediaType === 'video' || file.type.startsWith('video/');
        if (isVideo) {
          void this.insertVideoFromSrc(res.url, x, y).then(() => this.closeImageModal());
        } else {
          void this.insertImageFromSrc(res.url, x, y).then(() => this.closeImageModal());
        }
      },
      error: (err) => {
        this.imageUploading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private uploadAndInsertImage(file: File, x?: number, y?: number): void {
    this.uploadAndInsertMedia(file, x, y);
  }

  private uploadAndApplyToSelected(file: File): void {
    this.imageUploading.set(true);
    this.api.upload(file).subscribe({
      next: (res) => {
        this.imageUploading.set(false);
        const isVideo = res.mediaType === 'video' || file.type.startsWith('video/');
        if (isVideo) {
          void this.applyVideoSrcToSelected(res.url);
        } else {
          void this.applyImageSrcToSelected(res.url);
        }
        this.closeImageModal();
      },
      error: (err) => {
        this.imageUploading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private async insertImageFromSrc(src: string, x?: number, y?: number): Promise<void> {
    const size = await this.probeImageSize(src);
    const el: SlideElement = {
      id: newClientId('el'),
      type: 'image',
      x: x ?? Math.round((SLIDE_W - size.w) / 2),
      y: y ?? Math.round((SLIDE_H - size.h) / 2),
      w: size.w,
      h: size.h,
      rotation: 0,
      z: this.nextZ(),
      props: { src, opacity: 1 }
    };
    this.pushElement(el);
  }

  private insertVideoFromSrc(src: string, x?: number, y?: number): Promise<void> {
    const w = 640;
    const h = 360;
    const el: SlideElement = {
      id: newClientId('el'),
      type: 'video',
      x: x ?? Math.round((SLIDE_W - w) / 2),
      y: y ?? Math.round((SLIDE_H - h) / 2),
      w,
      h,
      rotation: 0,
      z: this.nextZ(),
      props: { src, autoplay: false, loop: false, muted: true }
    };
    this.pushElement(el);
    return Promise.resolve();
  }

  private applyVideoSrcToSelected(src: string): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.patchElement(id, (el) => {
      if (el.type !== 'video') {
        return el;
      }
      return {
        ...el,
        props: { ...(el.props as VideoProps), src }
      };
    });
    this.markDirty();
  }

  private async applyImageSrcToSelected(src: string): Promise<void> {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    const size = await this.probeImageSize(src);
    this.patchElement(id, (el) => {
      if (el.type !== 'image') {
        return el;
      }
      return {
        ...el,
        w: size.w,
        h: size.h,
        props: { ...(el.props as ImageProps), src }
      };
    });
    this.markDirty();
  }

  private normalizeImageSrc(raw: string): string | null {
    const value = raw.trim();
    if (!value) {
      return null;
    }
    if (value.startsWith('/')) {
      return value;
    }
    try {
      const url = new URL(value);
      if (url.protocol === 'http:' || url.protocol === 'https:') {
        return value;
      }
    } catch {
      return null;
    }
    return null;
  }

  private probeImageSize(src: string): Promise<{ w: number; h: number }> {
    return this.probeNaturalSize(src).then((natural) => {
      const maxW = 520;
      const maxH = 380;
      const scale = Math.min(1, maxW / natural.w, maxH / natural.h);
      return {
        w: Math.max(80, Math.round(natural.w * scale)),
        h: Math.max(60, Math.round(natural.h * scale))
      };
    });
  }

  private probeNaturalSize(src: string): Promise<{ w: number; h: number }> {
    return new Promise((resolve) => {
      const img = new Image();
      img.onload = () => {
        resolve({
          w: img.naturalWidth || 400,
          h: img.naturalHeight || 280
        });
      };
      img.onerror = () => resolve({ w: 400, h: 280 });
      img.src = resolveMediaUrl(src);
    });
  }

  private dropPosition(ev: DragEvent): { x: number; y: number } {
    const target = ev.currentTarget as HTMLElement | null;
    const slide = target?.querySelector('.slide') as HTMLElement | null;
    if (!slide) {
      return { x: 280, y: 120 };
    }
    const rect = slide.getBoundingClientRect();
    const z = this.zoom() || 1;
    const x = Math.round((ev.clientX - rect.left) / z - 80);
    const y = Math.round((ev.clientY - rect.top) / z - 60);
    return {
      x: Math.max(0, Math.min(SLIDE_W - 80, x)),
      y: Math.max(0, Math.min(SLIDE_H - 80, y))
    };
  }

  startDrag(
    ev: PointerEvent,
    id: string,
    mode: 'move' | 'resize',
    resizeDir: 'se' | 'sw' | 'ne' | 'nw' | 'e' | 'w' | 'n' | 's' = 'se'
  ): void {
    if (this.editingText()) {
      return;
    }
    ev.preventDefault();
    ev.stopPropagation();
    const el = this.activeSlide()?.elements.find((e) => e.id === id);
    if (!el) {
      return;
    }
    this.selectedId.set(id);
    this.drag = {
      id,
      mode,
      resizeDir,
      startX: ev.clientX,
      startY: ev.clientY,
      origX: el.x,
      origY: el.y,
      origW: el.w,
      origH: el.h,
      aspect: el.w / Math.max(el.h, 1),
      lockAspect: el.type === 'image' && !ev.altKey
    };
    (ev.currentTarget as HTMLElement).setPointerCapture?.(ev.pointerId);
  }

  onPointerMove(ev: PointerEvent): void {
    if (!this.drag) {
      return;
    }
    const z = this.zoom() || 1;
    const dx = (ev.clientX - this.drag.startX) / z;
    const dy = (ev.clientY - this.drag.startY) / z;
    if (this.drag.mode === 'move') {
      this.patchElement(this.drag.id, (el) => ({
        ...el,
        x: Math.round(this.drag!.origX + dx),
        y: Math.round(this.drag!.origY + dy)
      }));
      return;
    }

    const dir = this.drag.resizeDir ?? 'se';
    let x = this.drag.origX;
    let y = this.drag.origY;
    let w = this.drag.origW;
    let h = this.drag.origH;

    if (dir.includes('e')) {
      w = this.drag.origW + dx;
    }
    if (dir.includes('w')) {
      w = this.drag.origW - dx;
      x = this.drag.origX + dx;
    }
    if (dir.includes('s')) {
      h = this.drag.origH + dy;
    }
    if (dir.includes('n')) {
      h = this.drag.origH - dy;
      y = this.drag.origY + dy;
    }

    w = Math.max(40, Math.round(w));
    h = Math.max(24, Math.round(h));

    if (this.drag.lockAspect && this.drag.aspect) {
      const aspect = this.drag.aspect;
      if (dir === 'e' || dir === 'w') {
        h = Math.max(24, Math.round(w / aspect));
      } else if (dir === 'n' || dir === 's') {
        w = Math.max(40, Math.round(h * aspect));
      } else if (Math.abs(dx) >= Math.abs(dy)) {
        h = Math.max(24, Math.round(w / aspect));
      } else {
        w = Math.max(40, Math.round(h * aspect));
      }

      if (dir.includes('w')) {
        x = this.drag.origX + (this.drag.origW - w);
      }
      if (dir.includes('n')) {
        y = this.drag.origY + (this.drag.origH - h);
      }
    }

    this.patchElement(this.drag.id, (el) => ({
      ...el,
      x: Math.round(x),
      y: Math.round(y),
      w,
      h
    }));
  }

  endDrag(): void {
    if (this.drag) {
      this.drag = null;
      this.markDirty();
    }
  }

  deleteSelected(): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.updateActive((slide) => ({
      ...slide,
      elements: slide.elements.filter((e) => e.id !== id)
    }));
    this.selectedId.set(null);
    this.markDirty();
  }

  duplicateElement(): void {
    const sel = this.selected();
    if (!sel) {
      return;
    }
    const copy: SlideElement = {
      ...sel,
      id: newClientId('el'),
      x: sel.x + 24,
      y: sel.y + 24,
      z: this.nextZ(),
      props: { ...sel.props } as SlideElement['props']
    };
    this.pushElement(copy);
  }

  copyElement(): void {
    const sel = this.selected();
    if (sel) {
      this.clipboard = structuredClone(sel);
    }
  }

  pasteElement(): void {
    if (!this.clipboard) {
      return;
    }
    const copy: SlideElement = {
      ...structuredClone(this.clipboard),
      id: newClientId('el'),
      x: this.clipboard.x + 28,
      y: this.clipboard.y + 28,
      z: this.nextZ()
    };
    this.pushElement(copy);
  }

  nudgeSelected(dx: number, dy: number): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.patchElement(id, (el) => ({ ...el, x: el.x + dx, y: el.y + dy }));
    this.markDirty();
  }

  align(kind: 'left' | 'center' | 'right' | 'top' | 'middle' | 'bottom'): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.patchElement(id, (el) => {
      let x = el.x;
      let y = el.y;
      if (kind === 'left') {
        x = 40;
      }
      if (kind === 'center') {
        x = Math.round((SLIDE_W - el.w) / 2);
      }
      if (kind === 'right') {
        x = SLIDE_W - el.w - 40;
      }
      if (kind === 'top') {
        y = 40;
      }
      if (kind === 'middle') {
        y = Math.round((SLIDE_H - el.h) / 2);
      }
      if (kind === 'bottom') {
        y = SLIDE_H - el.h - 40;
      }
      return { ...el, x, y };
    });
    this.markDirty();
  }

  updateTextProp<K extends keyof TextProps>(key: K, value: TextProps[K]): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.patchElement(id, (el) => {
      if (el.type !== 'text') {
        return el;
      }
      return { ...el, props: { ...(el.props as TextProps), [key]: value } };
    });
    this.markDirty();
  }

  updateShapeProp<K extends keyof ShapeProps>(key: K, value: ShapeProps[K]): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.patchElement(id, (el) => {
      if (el.type !== 'shape') {
        return el;
      }
      return { ...el, props: { ...(el.props as ShapeProps), [key]: value } };
    });
    this.markDirty();
  }

  updateBgColor(color: string): void {
    this.updateActive((s) => ({
      ...s,
      background: { ...s.background, type: 'solid', color }
    }));
    this.markDirty();
  }

  updateBgGradient(c1: string, c2: string): void {
    this.updateActive((s) => ({
      ...s,
      background: { type: 'gradient', color: c1, color2: c2 }
    }));
    this.markDirty();
  }

  updateNotes(notes: string): void {
    this.updateActive((s) => ({ ...s, notes }));
    this.markDirty();
  }

  updateSlideTitle(title: string): void {
    this.updateActive((s) => ({ ...s, title }));
    this.markDirty();
  }

  present(): void {
    this.saveNow();
    this.skipUnload = true;
    void this.router.navigate(['/teacher/presentations', this.presentationId(), 'present']);
  }

  exportDeck(format: 'xlsx' | 'docx' | 'pptx'): void {
    const id = this.presentationId();
    if (!id) return;
    this.saveNow();
    this.api.exportFile(id, format).subscribe({
      next: (blob) => {
        const name = (this.title().trim() || 'presentacion').replace(/[<>:"/\\|?*]+/g, '-');
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${name}.${format}`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
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

  private pushElement(el: SlideElement): void {
    this.updateActive((slide) => ({
      ...slide,
      elements: [...slide.elements, el]
    }));
    this.selectedId.set(el.id);
    this.markDirty();
  }

  private nextZ(): number {
    const els = this.activeSlide()?.elements ?? [];
    return els.reduce((m, e) => Math.max(m, e.z), 0) + 1;
  }

  private patchElement(id: string, fn: (el: SlideElement) => SlideElement): void {
    this.updateActive((slide) => ({
      ...slide,
      elements: slide.elements.map((e) => (e.id === id ? fn(e) : e))
    }));
  }

  private updateActive(fn: (s: EditorSlide) => EditorSlide): void {
    const slides = [...this.slides()];
    const i = this.activeIndex();
    if (!slides[i]) {
      return;
    }
    slides[i] = fn(slides[i]);
    this.slides.set(slides);
  }

  private draftKey(id: number): string {
    return `cale.presentation.draft.${id}`;
  }

  private persistLocalDraft(): void {
    const id = this.presentationId();
    if (!id) {
      return;
    }
    try {
      localStorage.setItem(
        this.draftKey(id),
        JSON.stringify({
          title: this.title(),
          description: this.description(),
          category: this.category(),
          groupId: this.groupId(),
          slides: this.slides(),
          at: Date.now()
        })
      );
    } catch {
      /* quota */
    }
  }

  private clearLocalDraft(id: number): void {
    localStorage.removeItem(this.draftKey(id));
  }

  private tryRestoreLocalDraft(id: number): void {
    try {
      const raw = localStorage.getItem(this.draftKey(id));
      if (!raw) {
        return;
      }
      const draft = JSON.parse(raw) as {
        title: string;
        description: string;
        category: string;
        groupId: number | null;
        slides: EditorSlide[];
        at: number;
      };
      if (!draft.slides?.length) {
        return;
      }
      if (confirm('Hay un borrador local más reciente. ¿Restaurar cambios no sincronizados?')) {
        this.title.set(draft.title);
        this.description.set(draft.description);
        this.category.set(draft.category);
        this.groupId.set(draft.groupId);
        this.slides.set(draft.slides);
        this.markDirty();
      } else {
        this.clearLocalDraft(id);
      }
    } catch {
      /* ignore */
    }
  }
}
