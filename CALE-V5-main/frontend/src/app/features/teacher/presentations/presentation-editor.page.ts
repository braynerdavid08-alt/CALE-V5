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
import {
  Observable,
  catchError,
  finalize,
  firstValueFrom,
  map,
  of,
  shareReplay
} from 'rxjs';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { mapApiError } from '../../../core/http/map-api-error';
import { BRAND } from '../../../core/brand';
import { PresentationApi } from './presentation.api';
import {
  OFFICE_EXPORT_LIMITS,
  OFFICE_IMPORT_LIMITS,
  downloadBlob,
  openPresentationPrintPdf,
  renderSlideToPngBlob
} from './presentation-export-client';
import {
  EditorSlide,
  ImageCrop,
  ImageProps,
  LineProps,
  PRESENTATION_CATEGORIES,
  PRESENTATION_MEDIA_MAX_BYTES,
  PresentationDeckSummary,
  QuestionProps,
  SLIDE_H,
  SLIDE_W,
  ShapeKind,
  ShapeProps,
  SlideElement,
  SlideQuestionKind,
  TEMPLATE_OPTIONS,
  TextProps,
  VideoProps,
  backgroundCss,
  defaultQuestionProps,
  dtoToEditorSlides,
  hasImageCrop,
  imageElementStyles,
  imageSrc,
  newClientId,
  normalizeImageCrop,
  serializeBackground,
  shapeClipPath,
  summarizePresentationSlides,
  unlockImportedPhotoSlide
} from './presentation.models';
import { buildSlideFromTemplate, reassignElementIds } from './presentation-slide-templates';
import {
  CatalogSignRef,
  SIGN_FAMILY_FILTERS,
  SignFamilyFilter,
  buildCatalogSignImageElement,
  filterCatalogSigns,
  signFamilyShortLabel
} from './presentation-vial-resources';
import { PlayApi } from '../../play/api/play.api';
import { SlidePreviewComponent } from './slide-preview.component';

type SaveState = 'idle' | 'dirty' | 'saving' | 'saved' | 'offline' | 'error';

interface EditorSnapshot {
  slides: EditorSlide[];
  title: string;
  description: string;
  category: string;
  activeIndex: number;
  selectedIds: string[];
}

@Component({
  selector: 'app-presentation-editor-page',
  standalone: true,
  imports: [FormsModule, RouterLink, NgStyle, DecimalPipe, SlidePreviewComponent],
  templateUrl: './presentation-editor.page.html',
  styleUrl: './presentation-editor.page.css'
})
export class PresentationEditorPage implements OnInit, OnDestroy {
  @ViewChild('canvasHost') canvasHost?: ElementRef<HTMLElement>;
  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;
  @ViewChild('videoInput') videoInput?: ElementRef<HTMLInputElement>;
  @ViewChild('questionImageInput') questionImageInput?: ElementRef<HTMLInputElement>;

  private readonly api = inject(PresentationApi);
  private readonly playApi = inject(PlayApi);
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
  readonly selectedIds = signal<string[]>([]);
  readonly selectedId = computed(() => this.selectedIds()[0] ?? null);
  readonly zoom = signal(0.35);
  readonly autoFitZoom = signal(true);
  readonly saveState = signal<SaveState>('idle');
  readonly lastSavedAt = signal<number | null>(null);
  readonly editingText = signal(false);
  readonly showImageModal = signal(false);
  readonly showExportMenu = signal(false);
  readonly showShortcuts = signal(false);
  readonly exportingPng = signal(false);
  readonly cropEditing = signal(false);
  readonly toolbarMenu = signal<'shapes' | 'insert' | 'align' | 'arrange' | null>(null);
  readonly propsTab = signal<'slide' | 'element' | 'layers' | 'deck'>('slide');
  readonly propsDrawerOpen = signal(false);
  readonly officeExportLimits = OFFICE_EXPORT_LIMITS;
  readonly officeImportLimits = OFFICE_IMPORT_LIMITS;
  private layerDragFrom: string | null = null;
  readonly imageUrlInput = signal('');
  readonly imageUploading = signal(false);
  readonly imageModalReplace = signal(false);
  readonly brokenMediaIds = signal<Set<string>>(new Set());
  readonly addSlideTemplate = signal('blank');
  readonly canUndo = signal(false);
  readonly canRedo = signal(false);
  readonly showSignPicker = signal(false);
  readonly resourcesTrayOpen = signal(true);
  readonly catalogSigns = signal<CatalogSignRef[]>([]);
  readonly vialFamilyFilter = signal<SignFamilyFilter>('all');
  readonly vialSearch = signal('');
  /** Resizable chrome (px); persisted locally. */
  readonly slidesPanelW = signal(220);
  readonly propsPanelW = signal(320);
  readonly trayH = signal(148);
  private panelResize: {
    kind: 'slides' | 'props' | 'tray';
    startX: number;
    startY: number;
    startW: number;
    startH: number;
  } | null = null;
  readonly snapEnabled = signal(true);
  readonly snapGuideX = signal<number | null>(null);
  readonly snapGuideY = signal<number | null>(null);
  readonly importBanner = signal<PresentationDeckSummary | null>(null);
  readonly signFamilyFilters = SIGN_FAMILY_FILTERS;
  readonly filteredCatalogSigns = computed(() =>
    filterCatalogSigns(this.catalogSigns(), this.vialFamilyFilter(), this.vialSearch())
  );
  private elementClipboard: SlideElement[] = [];

  @ViewChild('bgImageInput') bgImageInput?: ElementRef<HTMLInputElement>;

  readonly activeSlide = computed(() => this.slides()[this.activeIndex()] ?? null);
  readonly selected = computed(() => {
    const slide = this.activeSlide();
    const id = this.selectedId();
    if (!slide || !id) {
      return null;
    }
    return slide.elements.find((e) => e.id === id) ?? null;
  });
  readonly selectedCount = computed(() => this.selectedIds().length);
  readonly canGroup = computed(() => this.selectedIds().length >= 2);
  readonly canUngroup = computed(() => {
    const slide = this.activeSlide();
    if (!slide) {
      return false;
    }
    return this.selectedIds().some((id) => {
      const el = slide.elements.find((e) => e.id === id);
      return !!el?.groupId;
    });
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

  readonly propsHeading = computed(() => {
    switch (this.propsTab()) {
      case 'element':
        return this.selectedCount() > 1
          ? `${this.selectedCount()} objetos`
          : this.selected()
            ? this.elementTypeLabel(this.selected()!.type)
            : 'Objeto';
      case 'layers':
        return 'Orden de capas';
      case 'deck':
        return 'Presentación';
      default:
        return 'Esta diapositiva';
    }
  });

  readonly propsHint = computed(() => {
    switch (this.propsTab()) {
      case 'element':
        return this.selected()
          ? 'Ajusta tamaño, color y contenido del objeto marcado en el lienzo.'
          : 'Haz clic en un texto, imagen o forma del lienzo para editarlo aquí.';
      case 'layers':
        return 'Arriba = delante. Toca un ítem para seleccionarlo en el lienzo.';
      case 'deck':
        return 'Datos generales de toda la presentación (no de una sola diapositiva).';
      default:
        return 'Fondo, nombre y guion de la diapositiva que estás viendo.';
    }
  });

  readonly selectedText = computed(() => {
    const sel = this.selected();
    return sel?.type === 'text' && this.selectedCount() === 1 ? sel : null;
  });

  private autosaveTimer?: ReturnType<typeof setTimeout>;
  private labelTimer?: ReturnType<typeof setInterval>;
  private fitZoomTimer?: ReturnType<typeof setTimeout>;
  private saveInFlight: Observable<boolean> | null = null;
  private lastTextTapAt = 0;
  private lastTextTapId: string | null = null;
  private drag:
    | {
        id: string;
        ids: string[];
        origins: Record<string, { x: number; y: number; w: number; h: number }>;
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
  private skipUnload = false;
  private undoStack: EditorSnapshot[] = [];
  private redoStack: EditorSnapshot[] = [];
  private readonly historyMax = 50;

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
        this.maybeShowImportBanner();
        this.scheduleFitZoom();
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
    this.loadCatalogSigns();
    this.restorePanelLayout();
    this.labelTimer = setInterval(() => {
      if (this.saveState() === 'saved') {
        this.lastSavedAt.update((v) => v);
      }
    }, 1000);
  }

  private loadCatalogSigns(): void {
    this.playApi.signs().subscribe({
      next: (signs) => this.catalogSigns.set(signs ?? []),
      error: () => this.catalogSigns.set([])
    });
  }

  ngOnDestroy(): void {
    if (this.autosaveTimer) {
      clearTimeout(this.autosaveTimer);
    }
    if (this.labelTimer) {
      clearInterval(this.labelTimer);
    }
    if (this.fitZoomTimer) {
      clearTimeout(this.fitZoomTimer);
    }
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    if (this.autoFitZoom()) {
      this.scheduleFitZoom();
    }
  }

  @HostListener('window:paste', ['$event'])
  onPaste(ev: ClipboardEvent): void {
    if (this.editingText() || this.isTypingTarget(ev.target)) {
      return;
    }
    const items = ev.clipboardData?.items;
    if (!items?.length) {
      return;
    }
    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (item.type.startsWith('image/')) {
        ev.preventDefault();
        const file = item.getAsFile();
        if (file) {
          if (file.size > PRESENTATION_MEDIA_MAX_BYTES) {
            this.error.set('La imagen debe pesar 100 MB o menos.');
            return;
          }
          this.uploadAndInsertMedia(file);
        }
        return;
      }
    }
  }

  private isTypingTarget(target: EventTarget | null): boolean {
    const el = target as HTMLElement | null;
    if (!el) {
      return false;
    }
    const tag = el.tagName;
    return tag === 'INPUT' || tag === 'TEXTAREA' || el.isContentEditable;
  }

  openSignPicker(): void {
    if (!this.catalogSigns().length) {
      this.loadCatalogSigns();
    }
    this.showSignPicker.set(true);
  }

  closeSignPicker(): void {
    this.showSignPicker.set(false);
  }

  toggleResourcesTray(): void {
    this.resourcesTrayOpen.update((v) => !v);
    this.persistPanelLayout();
  }

  startPanelResize(kind: 'slides' | 'props' | 'tray', ev: PointerEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.panelResize = {
      kind,
      startX: ev.clientX,
      startY: ev.clientY,
      startW: kind === 'props' ? this.propsPanelW() : this.slidesPanelW(),
      startH: this.trayH()
    };
    (ev.currentTarget as HTMLElement).setPointerCapture?.(ev.pointerId);
  }

  private restorePanelLayout(): void {
    try {
      const raw = localStorage.getItem('cale-pres-editor-layout');
      if (!raw) {
        return;
      }
      const parsed = JSON.parse(raw) as {
        slides?: number;
        props?: number;
        tray?: number;
        trayOpen?: boolean;
      };
      if (parsed.slides) {
        this.slidesPanelW.set(this.clamp(parsed.slides, 160, 420));
      }
      if (parsed.props) {
        this.propsPanelW.set(this.clamp(parsed.props, 260, 520));
      }
      if (parsed.tray) {
        this.trayH.set(this.clamp(parsed.tray, 96, 320));
      }
      if (typeof parsed.trayOpen === 'boolean') {
        this.resourcesTrayOpen.set(parsed.trayOpen);
      }
    } catch {
      /* ignore */
    }
  }

  private persistPanelLayout(): void {
    try {
      localStorage.setItem(
        'cale-pres-editor-layout',
        JSON.stringify({
          slides: this.slidesPanelW(),
          props: this.propsPanelW(),
          tray: this.trayH(),
          trayOpen: this.resourcesTrayOpen()
        })
      );
    } catch {
      /* ignore */
    }
  }

  private clamp(n: number, min: number, max: number): number {
    return Math.min(max, Math.max(min, Math.round(n)));
  }

  setVialFamilyFilter(cat: SignFamilyFilter): void {
    this.vialFamilyFilter.set(cat);
  }

  signFamilyLabel(family: string): string {
    return signFamilyShortLabel(family);
  }

  insertCatalogSign(sign: CatalogSignRef, opts?: { closePicker?: boolean }): void {
    const el = buildCatalogSignImageElement(sign);
    el.z = this.nextZ();
    this.pushHistory();
    this.pushElement(el);
    this.setSelectedId(el.id);
    if (opts?.closePicker) {
      this.closeSignPicker();
    }
    this.markDirty();
  }

  updateRotation(degrees: number): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.pushHistory();
    this.patchElement(id, (el) => ({ ...el, rotation: degrees }));
    this.markDirty();
  }

  toggleSnap(): void {
    this.snapEnabled.update((v) => !v);
  }

  toggleToolbarMenu(menu: 'shapes' | 'insert' | 'align' | 'arrange', ev?: Event): void {
    ev?.stopPropagation();
    this.showExportMenu.set(false);
    this.toolbarMenu.update((current) => (current === menu ? null : menu));
  }

  closeFloatingMenus(): void {
    this.toolbarMenu.set(null);
    this.showExportMenu.set(false);
  }

  toggleShortcuts(ev?: Event): void {
    ev?.stopPropagation();
    this.closeFloatingMenus();
    this.showShortcuts.update((v) => !v);
  }

  toggleExportMenu(ev?: Event): void {
    ev?.stopPropagation();
    this.toolbarMenu.set(null);
    this.showExportMenu.update((v) => !v);
  }

  setPropsTab(tab: 'slide' | 'element' | 'layers' | 'deck'): void {
    this.propsTab.set(tab);
  }

  openPropsDrawer(tab?: 'slide' | 'element' | 'layers' | 'deck'): void {
    if (tab) {
      this.propsTab.set(tab);
    }
    this.propsDrawerOpen.set(true);
  }

  closePropsDrawer(): void {
    this.propsDrawerOpen.set(false);
  }

  togglePropsDrawer(): void {
    this.propsDrawerOpen.update((v) => !v);
  }

  elementTypeLabel(type: string): string {
    switch (type) {
      case 'text':
        return 'Texto';
      case 'image':
        return 'Imagen';
      case 'video':
        return 'Video';
      case 'shape':
        return 'Forma';
      case 'line':
        return 'Línea';
      case 'arrow':
        return 'Flecha';
      case 'question':
        return 'Pregunta en vivo';
      default:
        return type;
    }
  }

  private snapCoord(value: number, grid = 8): number {
    if (!this.snapEnabled()) {
      return Math.round(value);
    }
    return Math.round(value / grid) * grid;
  }

  private snapBox(
    x: number,
    y: number,
    w: number,
    h: number,
    excludeIds: string[] = []
  ): { x: number; y: number; w: number; h: number } {
    let nx = this.snapEnabled() ? this.snapCoord(x) : Math.round(x);
    let ny = this.snapEnabled() ? this.snapCoord(y) : Math.round(y);
    let guideX: number | null = null;
    let guideY: number | null = null;
    if (!this.snapEnabled()) {
      this.snapGuideX.set(null);
      this.snapGuideY.set(null);
      return this.clampBoxToCanvas(nx, ny, w, h);
    }

    const threshold = 10;
    const trySnapAxis = (
      movingEdges: number[],
      targetEdges: number[],
      apply: (delta: number, target: number) => void
    ): boolean => {
      for (const m of movingEdges) {
        for (const t of targetEdges) {
          if (Math.abs(m - t) <= threshold) {
            apply(t - m, t);
            return true;
          }
        }
      }
      return false;
    };

    const mx = [x, x + w / 2, x + w];
    const my = [y, y + h / 2, y + h];
    trySnapAxis(mx, [0, SLIDE_W / 2, SLIDE_W], (delta, target) => {
      nx = Math.round(x + delta);
      guideX = Math.round(target);
    });
    trySnapAxis(my, [0, SLIDE_H / 2, SLIDE_H], (delta, target) => {
      ny = Math.round(y + delta);
      guideY = Math.round(target);
    });

    const others =
      this.activeSlide()?.elements.filter(
        (e) => !e.hidden && !excludeIds.includes(e.id)
      ) ?? [];
    for (const o of others) {
      trySnapAxis(mx, [o.x, o.x + o.w / 2, o.x + o.w], (delta, target) => {
        nx = Math.round(x + delta);
        guideX = Math.round(target);
      });
      trySnapAxis(my, [o.y, o.y + o.h / 2, o.y + o.h], (delta, target) => {
        ny = Math.round(y + delta);
        guideY = Math.round(target);
      });
    }

    this.snapGuideX.set(guideX);
    this.snapGuideY.set(guideY);
    return this.clampBoxToCanvas(nx, ny, w, h);
  }

  /** Mantiene el elemento dentro del lienzo fijo 1920×1080. */
  private clampBoxToCanvas(
    x: number,
    y: number,
    w: number,
    h: number
  ): { x: number; y: number; w: number; h: number } {
    const maxW = Math.max(40, Math.min(Math.round(w), SLIDE_W));
    const maxH = Math.max(24, Math.min(Math.round(h), SLIDE_H));
    const nx = Math.max(0, Math.min(Math.round(x), SLIDE_W - maxW));
    const ny = Math.max(0, Math.min(Math.round(y), SLIDE_H - maxH));
    return { x: nx, y: ny, w: maxW, h: maxH };
  }

  clearSnapGuides(): void {
    this.snapGuideX.set(null);
    this.snapGuideY.set(null);
  }

  copySelectedElement(): void {
    const slide = this.activeSlide();
    const ids = this.selectedIds();
    if (!slide || !ids.length) {
      return;
    }
    this.elementClipboard = ids
      .map((id) => slide.elements.find((e) => e.id === id))
      .filter((e): e is SlideElement => !!e)
      .map((e) => structuredClone(e));
  }

  pasteElementClipboard(): void {
    if (!this.elementClipboard.length) {
      return;
    }
    this.pushHistory();
    const pastedIds: string[] = [];
    let z = this.nextZ();
    for (const item of this.elementClipboard) {
      const copy: SlideElement = {
        ...structuredClone(item),
        id: newClientId('el'),
        x: item.x + 24,
        y: item.y + 24,
        z: z++,
        groupId: null
      };
      this.updateActive((slide) => ({
        ...slide,
        elements: [...slide.elements, copy]
      }));
      pastedIds.push(copy.id);
    }
    this.selectedIds.set(pastedIds);
    this.markDirty();
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

  @HostListener('document:click')
  onDocumentClick(): void {
    this.closeFloatingMenus();
  }

  @HostListener('window:keydown', ['$event'])
  onKey(ev: KeyboardEvent): void {
    if (ev.key === 'Escape') {
      if (this.showShortcuts()) {
        this.showShortcuts.set(false);
        return;
      }
      if (this.toolbarMenu() || this.showExportMenu()) {
        this.closeFloatingMenus();
        return;
      }
      if (this.propsDrawerOpen()) {
        this.closePropsDrawer();
        return;
      }
      if (this.showSignPicker()) {
        this.closeSignPicker();
        return;
      }
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
    if (meta && ev.key.toLowerCase() === 'c' && this.selectedId()) {
      ev.preventDefault();
      this.copySelectedElement();
      return;
    }
    if (meta && ev.key.toLowerCase() === 'v') {
      ev.preventDefault();
      this.pasteElementClipboard();
      return;
    }
    if (meta && ev.key.toLowerCase() === 'z' && !ev.shiftKey) {
      ev.preventDefault();
      this.undo();
      return;
    }
    if (meta && (ev.key.toLowerCase() === 'y' || (ev.key.toLowerCase() === 'z' && ev.shiftKey))) {
      ev.preventDefault();
      this.redo();
      return;
    }
    if (ev.key === 'F5') {
      ev.preventDefault();
      this.present();
      return;
    }
    if (ev.key === 'PageDown' && !meta) {
      ev.preventDefault();
      this.selectSlide(Math.min(this.slides().length - 1, this.activeIndex() + 1));
      return;
    }
    if (ev.key === 'PageUp' && !meta) {
      ev.preventDefault();
      this.selectSlide(Math.max(0, this.activeIndex() - 1));
      return;
    }
    if (meta && ev.key.toLowerCase() === 's') {
      ev.preventDefault();
      this.saveNow(false).subscribe();
      return;
    }
    if (meta && ev.key.toLowerCase() === 'g') {
      ev.preventDefault();
      if (ev.shiftKey) {
        this.ungroupSelected();
      } else {
        this.groupSelected();
      }
      return;
    }
    if (meta && ev.key.toLowerCase() === 'a') {
      ev.preventDefault();
      this.selectAllOnSlide();
      return;
    }
    if (meta && ev.key.toLowerCase() === 'd') {
      ev.preventDefault();
      this.duplicateElement();
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

  dismissImportBanner(): void {
    this.importBanner.set(null);
  }

  private maybeShowImportBanner(): void {
    const state = (typeof history !== 'undefined' ? history.state : null) as {
      importSummary?: boolean;
    } | null;
    if (!state?.importSummary) {
      return;
    }
    this.importBanner.set(summarizePresentationSlides(this.slides()));
    try {
      const next = { ...state };
      delete next.importSummary;
      history.replaceState(next, '');
    } catch {
      /* ignore */
    }
  }

  private scheduleFitZoom(): void {
    if (this.fitZoomTimer) {
      clearTimeout(this.fitZoomTimer);
    }
    // Esperar a que Angular pinte el canvas (#canvasHost) tras quitar el loading.
    this.fitZoomTimer = setTimeout(() => this.fitZoom(), 50);
    setTimeout(() => this.fitZoom(), 200);
  }

  fitZoom(): void {
    this.autoFitZoom.set(true);
    const host = this.canvasHost?.nativeElement;
    if (!host || host.clientWidth < 40 || host.clientHeight < 40) {
      const approx = Math.min(
        (window.innerWidth - 420) / SLIDE_W,
        (window.innerHeight - 220) / SLIDE_H
      );
      this.zoom.set(Math.max(0.12, Math.min(1, approx || 0.35)));
      return;
    }
    const pad = 32;
    const zx = (host.clientWidth - pad) / SLIDE_W;
    const zy = (host.clientHeight - pad) / SLIDE_H;
    this.zoom.set(Math.max(0.12, Math.min(1, Math.min(zx, zy))));
    host.scrollTop = 0;
    host.scrollLeft = 0;
  }

  setZoom(v: number): void {
    this.autoFitZoom.set(false);
    this.zoom.set(Math.max(0.12, Math.min(1.5, v)));
  }

  markDirty(): void {
    this.saveState.set('dirty');
    this.persistLocalDraft();
    if (this.autosaveTimer) {
      clearTimeout(this.autosaveTimer);
    }
    this.autosaveTimer = setTimeout(() => this.saveNow(true).subscribe(), 1400);
  }

  private cloneSnapshot(): EditorSnapshot {
    return {
      slides: structuredClone(this.slides()),
      title: this.title(),
      description: this.description(),
      category: this.category(),
      activeIndex: this.activeIndex(),
      selectedIds: [...this.selectedIds()]
    };
  }

  private refreshHistoryFlags(): void {
    this.canUndo.set(this.undoStack.length > 0);
    this.canRedo.set(this.redoStack.length > 0);
  }

  private pushHistory(): void {
    this.undoStack.push(this.cloneSnapshot());
    if (this.undoStack.length > this.historyMax) {
      this.undoStack.shift();
    }
    this.redoStack = [];
    this.refreshHistoryFlags();
  }

  undo(): void {
    if (!this.undoStack.length) {
      return;
    }
    this.redoStack.push(this.cloneSnapshot());
    const snap = this.undoStack.pop()!;
    this.restoreSnapshot(snap);
    this.refreshHistoryFlags();
    this.markDirty();
  }

  redo(): void {
    if (!this.redoStack.length) {
      return;
    }
    this.undoStack.push(this.cloneSnapshot());
    const snap = this.redoStack.pop()!;
    this.restoreSnapshot(snap);
    this.refreshHistoryFlags();
    this.markDirty();
  }

  private restoreSnapshot(snap: EditorSnapshot): void {
    this.slides.set(snap.slides);
    this.title.set(snap.title);
    this.description.set(snap.description);
    this.category.set(snap.category);
    this.activeIndex.set(snap.activeIndex);
    this.selectedIds.set([...(snap.selectedIds ?? [])]);
    this.editingText.set(false);
  }

  private deriveThumbnailUrl(): string | null {
    for (const slide of this.slides()) {
      const image = slide.elements.find((e) => e.type === 'image');
      if (image) {
        return (image.props as ImageProps).src;
      }
    }
    return null;
  }

  /** Persist to server. Returns whether the latest content is safely stored. */
  saveNow(auto = false): Observable<boolean> {
    const id = this.presentationId();
    if (!id) {
      return of(false);
    }
    if (this.saveInFlight) {
      return this.saveInFlight;
    }
    const state = this.saveState();
    if (auto && state !== 'dirty' && state !== 'offline' && state !== 'error') {
      return of(true);
    }
    if (!auto && (state === 'saved' || state === 'idle')) {
      return of(true);
    }

    if (this.autosaveTimer) {
      clearTimeout(this.autosaveTimer);
      this.autosaveTimer = undefined;
    }

    this.saveState.set('saving');
    const payload = {
      title: this.title().trim() || 'Sin título',
      description: this.description().trim() || null,
      category: this.category(),
      groupId: this.groupId(),
      thumbnailUrl: this.deriveThumbnailUrl(),
      slides: this.slides().map((s) => ({
        id: s.id ?? null,
        title: s.title,
        notes: s.notes || null,
        backgroundJson: serializeBackground(s),
        elementsJson: JSON.stringify(s.elements)
      }))
    };

    this.saveInFlight = this.api.saveDocument(id, payload).pipe(
      map((detail) => {
        this.slides.set(dtoToEditorSlides(detail));
        this.saveState.set('saved');
        this.lastSavedAt.set(Date.now());
        this.clearLocalDraft(id);
        if (!auto) {
          this.error.set(null);
        }
        return true;
      }),
      catchError((err) => {
        this.persistLocalDraft();
        if (!navigator.onLine) {
          this.saveState.set('offline');
        } else {
          this.saveState.set('error');
          this.error.set(mapApiError(err));
        }
        return of(false);
      }),
      finalize(() => {
        this.saveInFlight = null;
      }),
      shareReplay(1)
    );
    return this.saveInFlight;
  }

  private async flushSave(actionLabel: string): Promise<boolean> {
    if (this.editingText()) {
      this.finishTextEdit();
    }
    // Finish any autosave in flight, then force one more pass with the latest canvas.
    if (this.saveInFlight) {
      await firstValueFrom(this.saveInFlight);
    }
    this.saveState.set('dirty');
    const ok = await firstValueFrom(this.saveNow(false));
    if (!ok) {
      this.error.set(
        this.error() || `No se pudo guardar. Revisa la conexión e inténtalo de nuevo antes de ${actionLabel}.`
      );
    }
    return ok;
  }

  selectSlide(i: number): void {
    this.activeIndex.set(i);
    this.setSelectedId(null);
    this.editingText.set(false);
    this.cropEditing.set(false);
  }

  addSlide(templateKey?: string): void {
    this.pushHistory();
    const key = templateKey ?? this.addSlideTemplate();
    const slides = [...this.slides()];
    const built = reassignElementIds(buildSlideFromTemplate(key, slides.length + 1));
    slides.splice(this.activeIndex() + 1, 0, built);
    this.slides.set(slides);
    this.activeIndex.set(this.activeIndex() + 1);
    this.setSelectedId(null);
    this.markDirty();
  }

  duplicateSlide(): void {
    const cur = this.activeSlide();
    if (!cur) {
      return;
    }
    this.pushHistory();
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
    this.pushHistory();
    const slides = [...this.slides()];
    slides.splice(this.activeIndex(), 1);
    this.slides.set(slides);
    this.activeIndex.set(Math.max(0, this.activeIndex() - 1));
    this.setSelectedId(null);
    this.markDirty();
  }

  moveSlide(from: number, to: number): void {
    if (to < 0 || to >= this.slides().length || from === to) {
      return;
    }
    this.pushHistory();
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
    const slide = this.activeSlide();
    if (!slide) {
      return;
    }
    const el = slide.elements.find((e) => e.id === id);
    if (!el) {
      return;
    }
    const peers = el.groupId
      ? slide.elements.filter((e) => e.groupId === el.groupId).map((e) => e.id)
      : [id];
    const multi = !!(ev && ((ev as MouseEvent).shiftKey || (ev as MouseEvent).ctrlKey || (ev as MouseEvent).metaKey));
    this.editingText.set(false);
    if (multi) {
      const cur = new Set(this.selectedIds());
      const allSelected = peers.every((pid) => cur.has(pid));
      if (allSelected) {
        for (const pid of peers) {
          cur.delete(pid);
        }
      } else {
        for (const pid of peers) {
          cur.add(pid);
        }
      }
      const next = [...cur];
      if (next.includes(id)) {
        this.selectedIds.set([id, ...next.filter((x) => x !== id)]);
      } else {
        this.selectedIds.set(next);
      }
      return;
    }
    this.selectedIds.set([id, ...peers.filter((x) => x !== id)]);
    this.propsTab.set('element');
    if (typeof window !== 'undefined' && window.matchMedia('(max-width: 960px)').matches) {
      this.propsDrawerOpen.set(true);
    }
  }

  clearSelection(): void {
    this.selectedIds.set([]);
    this.editingText.set(false);
    this.cropEditing.set(false);
  }

  isSelected(id: string): boolean {
    return this.selectedIds().includes(id);
  }

  private setSelectedId(id: string | null): void {
    this.selectedIds.set(id ? [id] : []);
  }

  selectAllOnSlide(): void {
    const ids = (this.activeSlide()?.elements ?? []).map((e) => e.id);
    this.selectedIds.set(ids);
    this.editingText.set(false);
  }

  groupSelected(): void {
    const ids = this.selectedIds();
    if (ids.length < 2) {
      return;
    }
    this.pushHistory();
    const gid = newClientId('grp');
    this.updateActive((slide) => ({
      ...slide,
      elements: slide.elements.map((e) =>
        ids.includes(e.id) ? { ...e, groupId: gid } : e
      )
    }));
    this.markDirty();
  }

  ungroupSelected(): void {
    const ids = this.selectedIds();
    const slide = this.activeSlide();
    if (!ids.length || !slide) {
      return;
    }
    const groupIds = new Set(
      ids
        .map((id) => slide.elements.find((e) => e.id === id)?.groupId)
        .filter((g): g is string => !!g)
    );
    if (!groupIds.size) {
      return;
    }
    this.pushHistory();
    this.updateActive((s) => ({
      ...s,
      elements: s.elements.map((e) =>
        e.groupId && groupIds.has(e.groupId) ? { ...e, groupId: null } : e
      )
    }));
    this.markDirty();
  }

  isPhotoSlide(): boolean {
    const slide = this.activeSlide();
    if (!slide) {
      return false;
    }
    return !!slide.background.imageUrl && slide.background.type === 'image';
  }

  layerItems(): SlideElement[] {
    return [...(this.activeSlide()?.elements ?? [])].sort((a, b) => b.z - a.z);
  }

  /** Visible on the canvas (hidden layers stay in the Capas panel only). */
  canvasElements(slide: EditorSlide): SlideElement[] {
    return slide.elements.filter((e) => !e.hidden);
  }

  layerLabel(el: SlideElement): string {
    if (el.type === 'text') {
      const text = ((el.props as TextProps).text ?? '').replace(/\s+/g, ' ').trim();
      return text ? (text.length > 32 ? `${text.slice(0, 32)}…` : text) : 'Texto';
    }
    const labels: Record<string, string> = {
      image: 'Imagen',
      video: 'Video',
      shape: 'Figura',
      line: 'Línea',
      arrow: 'Flecha',
      question: 'Pregunta'
    };
    return labels[el.type] ?? el.type;
  }

  toggleElementHidden(id: string, ev?: Event): void {
    ev?.stopPropagation();
    this.pushHistory();
    this.patchElement(id, (el) => ({ ...el, hidden: !el.hidden }));
    if (this.selectedIds().includes(id)) {
      this.selectedIds.update((ids) => ids.filter((x) => x !== id));
      this.editingText.set(false);
    }
    this.markDirty();
  }

  /** Capas: al elegir un objeto oculto, se muestra de nuevo para poder editarlo. */
  revealAndSelect(id: string, ev: Event): void {
    ev.stopPropagation();
    const el = this.activeSlide()?.elements.find((e) => e.id === id);
    if (el?.hidden) {
      this.pushHistory();
      this.patchElement(id, (e) => ({ ...e, hidden: false }));
      this.markDirty();
    }
    this.selectElement(id, ev);
    this.setPropsTab('element');
  }

  toggleElementLocked(id: string, ev?: Event): void {
    ev?.stopPropagation();
    this.pushHistory();
    this.patchElement(id, (el) => ({ ...el, locked: !el.locked }));
    this.markDirty();
  }

  onLayerDragStart(ev: DragEvent, id: string): void {
    this.layerDragFrom = id;
    ev.dataTransfer?.setData('text/plain', id);
    ev.dataTransfer!.effectAllowed = 'move';
  }

  onLayerDrop(ev: DragEvent, targetId: string): void {
    ev.preventDefault();
    const fromId = this.layerDragFrom || ev.dataTransfer?.getData('text/plain');
    this.layerDragFrom = null;
    if (!fromId || fromId === targetId) {
      return;
    }
    const ordered = this.layerItems();
    const from = ordered.findIndex((e) => e.id === fromId);
    const to = ordered.findIndex((e) => e.id === targetId);
    if (from < 0 || to < 0) {
      return;
    }
    this.pushHistory();
    const next = [...ordered];
    const [item] = next.splice(from, 1);
    next.splice(to, 0, item);
    // Front of list = highest z
    const maxZ = next.length;
    const zById = new Map(next.map((e, i) => [e.id, maxZ - i]));
    this.updateActive((s) => ({
      ...s,
      elements: s.elements.map((e) => ({ ...e, z: zById.get(e.id) ?? e.z }))
    }));
    this.markDirty();
  }

  convertSelectedImageToBackground(): void {
    const sel = this.selected();
    const src = sel ? imageSrc(sel) : null;
    if (!sel || !src) {
      return;
    }
    this.pushHistory();
    this.updateActive((s) => ({
      ...s,
      background: { type: 'image', color: s.background.color || '#F7F9FC', imageUrl: src },
      elements: s.elements.filter((e) => e.id !== sel.id)
    }));
    this.setSelectedId(null);
    this.markDirty();
  }

  onCanvasDblClick(ev: MouseEvent): void {
    if ((ev.target as HTMLElement).closest('.el')) {
      return;
    }
    ev.preventDefault();
    ev.stopPropagation();
    const pos = this.dropPosition(ev as unknown as DragEvent);
    this.addTextAt(pos.x, pos.y);
  }

  onElementPointerDown(ev: PointerEvent, el: SlideElement): void {
    if (this.editingText() && this.selectedId() === el.id && el.type === 'text') {
      return;
    }
    if ((ev.target as HTMLElement).closest('.handle, .text-drag-handle')) {
      return;
    }

    ev.preventDefault();
    ev.stopPropagation();

    // Touch / stylus: second tap within 450ms opens text edit (no reliable dblclick).
    if (el.type === 'text' && (ev.pointerType === 'touch' || ev.pointerType === 'pen')) {
      const now = Date.now();
      if (this.lastTextTapId === el.id && now - this.lastTextTapAt < 450) {
        this.lastTextTapAt = 0;
        this.lastTextTapId = null;
        this.startEditText(el.id, ev);
        return;
      }
      this.lastTextTapAt = now;
      this.lastTextTapId = el.id;
    }

    // Mouse: one click selects (frame + handles). Double-click edits letters.
    this.editingText.set(false);
    this.selectElement(el.id, ev);
    this.startDrag(ev, el.id, 'move');
  }

  startEditText(id: string, ev: Event): void {
    ev.stopPropagation();
    const slideEl = this.activeSlide()?.elements.find((e) => e.id === id);
    if (!slideEl || slideEl.type !== 'text') {
      return;
    }
    if (!this.editingText()) {
      this.pushHistory();
    }
    const initialText = (slideEl.props as TextProps).text;
    this.setSelectedId(id);
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
    this.addTextAt(120, kind === 'title' ? 48 : 160, kind);
  }

  private addTextAt(x: number, y: number, kind: 'title' | 'subtitle' | 'body' = 'body'): void {
    this.pushHistory();
    const sizes = { title: 40, subtitle: 28, body: 20 };
    const el: SlideElement = {
      id: newClientId('el'),
      type: 'text',
      x: Math.max(16, Math.min(SLIDE_W - 160, x)),
      y: Math.max(16, Math.min(SLIDE_H - 80, y)),
      w: 700,
      h: kind === 'body' ? 120 : 70,
      rotation: 0,
      z: this.nextZ(),
      props: {
        text: kind === 'title' ? 'Título' : kind === 'subtitle' ? 'Subtítulo' : 'Texto',
        fontSize: sizes[kind],
        fontWeight: kind === 'body' ? 400 : 700,
        color: this.isPhotoSlide() ? '#FFFFFF' : '#0B1F33',
        align: 'left',
        fontFamily: 'Segoe UI, sans-serif'
      }
    };
    this.pushElement(el);
    queueMicrotask(() => this.startEditText(el.id, new Event('init')));
  }

  addShape(shape: ShapeKind): void {
    this.pushHistory();
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
    this.pushHistory();
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
    this.pushHistory();
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
        text: 'Luz Verde',
        fontSize: 18,
        fontWeight: 700,
        color: '#2BB0ED',
        align: 'left',
        fontFamily: 'Segoe UI, sans-serif'
      }
    };
    this.pushElement(el);
  }

  addQuestion(kind: SlideQuestionKind = 'mcq'): void {
    this.pushHistory();
    const el: SlideElement = {
      id: newClientId('el'),
      type: 'question',
      x: 220,
      y: 140,
      w: 1480,
      h: kind === 'vf' ? 420 : 560,
      rotation: 0,
      z: this.nextZ(),
      props: defaultQuestionProps(kind)
    };
    this.pushElement(el);
  }

  optionLetter(index: number): string {
    return String.fromCharCode(65 + index);
  }

  questionProps(el: SlideElement): QuestionProps {
    return el.props as QuestionProps;
  }

  updateQuestionProp(key: keyof QuestionProps, value: unknown): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.patchElement(id, (el) => {
      if (el.type !== 'question') {
        return el;
      }
      return { ...el, props: { ...(el.props as QuestionProps), [key]: value } };
    });
    this.markDirty();
  }

  setQuestionOption(index: number, value: string): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.patchElement(id, (el) => {
      if (el.type !== 'question') {
        return el;
      }
      const props = el.props as QuestionProps;
      const options = [...props.options];
      options[index] = value;
      return { ...el, props: { ...props, options } };
    });
    this.markDirty();
  }

  setQuestionKind(kind: SlideQuestionKind): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.pushHistory();
    this.patchElement(id, (el) => {
      if (el.type !== 'question') {
        return el;
      }
      const current = el.props as QuestionProps;
      const next = defaultQuestionProps(kind);
      return {
        ...el,
        h: kind === 'vf' ? Math.min(el.h, 420) : Math.max(el.h, 520),
        props: {
          ...next,
          text: current.text?.trim() ? current.text : next.text,
          explanation: current.explanation,
          topic: current.topic,
          autoOpen: current.autoOpen,
          imageUrl: current.imageUrl ?? null
        }
      };
    });
    this.markDirty();
  }

  triggerQuestionImage(): void {
    this.questionImageInput?.nativeElement.click();
  }

  onQuestionImageSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    if (file.size > PRESENTATION_MEDIA_MAX_BYTES) {
      this.error.set('La imagen debe pesar 100 MB o menos.');
      return;
    }
    if (!this.isImageFile(file)) {
      this.error.set('Elige una imagen jpg, png, gif o webp.');
      return;
    }
    this.imageUploading.set(true);
    this.api.upload(file).subscribe({
      next: (res) => {
        this.imageUploading.set(false);
        this.updateQuestionProp('imageUrl', res.url);
      },
      error: (err) => {
        this.imageUploading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  setQuestionImageFromUrl(raw: string): void {
    const src = this.normalizeImageSrc(raw);
    if (!src) {
      this.error.set('URL inválida. Usa https://... o /uploads/...');
      return;
    }
    this.updateQuestionProp('imageUrl', src);
  }

  clearQuestionImage(): void {
    this.updateQuestionProp('imageUrl', null);
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
      this.uploadAndInsertMedia(file, undefined, undefined, this.isVideoFile(file) ? 'video' : 'image');
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
    if (!this.isVideoFile(file)) {
      this.error.set('Elige un video mp4, webm o mov.');
      return;
    }
    this.uploadAndInsertMedia(file, undefined, undefined, 'video');
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
    const kind = this.isVideoFile(file) ? 'video' : this.isImageFile(file) ? 'image' : null;
    if (!kind) {
      this.error.set('Arrastra una imagen (jpg/png) o un video (mp4/webm/mov).');
      return;
    }
    this.uploadAndInsertMedia(file, pos.x, pos.y, kind);
  }

  updateImageProp<K extends keyof ImageProps>(key: K, value: ImageProps[K]): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.pushHistory();
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
    this.pushHistory();
    this.patchElement(id, (el) => {
      if (el.type !== 'video') {
        return el;
      }
      return { ...el, props: { ...(el.props as VideoProps), [key]: value } };
    });
    this.markDirty();
  }

  updateElementGeometry(key: 'x' | 'y' | 'w' | 'h', raw: number): void {
    const id = this.selectedId();
    if (!id || !Number.isFinite(raw)) {
      return;
    }
    this.pushHistory();
    this.patchElement(id, (el) => {
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

  /** @deprecated use updateElementGeometry */
  updateImageGeometry(key: 'x' | 'y' | 'w' | 'h', raw: number): void {
    this.updateElementGeometry(key, raw);
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

  private uploadAndInsertMedia(
    file: File,
    x?: number,
    y?: number,
    forceKind?: 'image' | 'video'
  ): void {
    this.imageUploading.set(true);
    this.api.upload(file).subscribe({
      next: (res) => {
        this.imageUploading.set(false);
        const isVideo =
          forceKind === 'video'
          || (forceKind !== 'image' && (res.mediaType === 'video' || this.isVideoFile(file)));
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
    this.uploadAndInsertMedia(file, x, y, 'image');
  }

  private uploadAndApplyToSelected(file: File): void {
    this.imageUploading.set(true);
    this.api.upload(file).subscribe({
      next: (res) => {
        this.imageUploading.set(false);
        const isVideo = res.mediaType === 'video' || this.isVideoFile(file);
        if (isVideo) {
          this.applyVideoSrcToSelected(res.url);
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

  private isVideoFile(file: File): boolean {
    if (file.type.toLowerCase().startsWith('video/')) {
      return true;
    }
    return /\.(mp4|webm|mov|m4v|avi)$/i.test(file.name);
  }

  private isImageFile(file: File): boolean {
    if (file.type.toLowerCase().startsWith('image/')) {
      return true;
    }
    return /\.(jpe?g|png|gif|webp|bmp)$/i.test(file.name);
  }

  private async insertImageFromSrc(src: string, x?: number, y?: number): Promise<void> {
    this.pushHistory();
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
    this.pushHistory();
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
    this.pushHistory();
    this.patchElement(id, (el) => {
      // Si había una imagen (póster), convertirla a video real.
      return {
        ...el,
        type: 'video',
        w: Math.max(el.w, 320),
        h: Math.max(el.h, 180),
        props: { src, autoplay: false, loop: false, muted: true }
      };
    });
    this.markDirty();
  }

  private async applyImageSrcToSelected(src: string): Promise<void> {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.pushHistory();
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
    const slide = this.activeSlide();
    const el = slide?.elements.find((e) => e.id === id);
    if (!el || !slide) {
      return;
    }
    if (!this.selectedIds().includes(id)) {
      this.selectElement(id, ev);
    }
    if (el.locked) {
      return;
    }
    this.pushHistory();
    const moveIds =
      mode === 'move'
        ? (this.selectedIds().length ? this.selectedIds() : [id]).filter((mid) => {
            const item = slide.elements.find((e) => e.id === mid);
            return !!item && !item.locked;
          })
        : [id];
    if (!moveIds.length) {
      return;
    }
    const origins: Record<string, { x: number; y: number; w: number; h: number }> = {};
    for (const mid of moveIds) {
      const item = slide.elements.find((e) => e.id === mid);
      if (item) {
        origins[mid] = { x: item.x, y: item.y, w: item.w, h: item.h };
      }
    }
    this.selectedIds.set([id, ...moveIds.filter((x) => x !== id)]);
    this.drag = {
      id,
      ids: moveIds,
      origins,
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
    if (this.panelResize) {
      const dx = ev.clientX - this.panelResize.startX;
      const dy = ev.clientY - this.panelResize.startY;
      if (this.panelResize.kind === 'slides') {
        this.slidesPanelW.set(this.clamp(this.panelResize.startW + dx, 160, 420));
      } else if (this.panelResize.kind === 'props') {
        this.propsPanelW.set(this.clamp(this.panelResize.startW - dx, 260, 520));
      } else {
        this.trayH.set(this.clamp(this.panelResize.startH - dy, 96, 320));
      }
      return;
    }
    if (!this.drag) {
      return;
    }
    const z = this.zoom() || 1;
    const dx = (ev.clientX - this.drag.startX) / z;
    const dy = (ev.clientY - this.drag.startY) / z;
    if (this.drag.mode === 'move') {
      const ids = this.drag.ids;
      this.updateActive((slide) => ({
        ...slide,
        elements: slide.elements.map((el) => {
          const orig = this.drag!.origins[el.id];
          if (!orig || !ids.includes(el.id)) {
            return el;
          }
          const pos = this.snapBox(orig.x + dx, orig.y + dy, orig.w, orig.h, ids);
          return { ...el, x: pos.x, y: pos.y, w: pos.w, h: pos.h };
        })
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

    const boxed = this.clampBoxToCanvas(x, y, w, h);
    this.patchElement(this.drag.id, (el) => ({
      ...el,
      x: boxed.x,
      y: boxed.y,
      w: boxed.w,
      h: boxed.h
    }));
  }

  endDrag(): void {
    if (this.panelResize) {
      this.panelResize = null;
      this.persistPanelLayout();
      return;
    }
    if (this.drag) {
      this.drag = null;
      this.clearSnapGuides();
      this.markDirty();
    }
  }

  deleteSelected(): void {
    const ids = new Set(this.selectedIds());
    if (!ids.size) {
      return;
    }
    this.pushHistory();
    this.updateActive((slide) => ({
      ...slide,
      elements: slide.elements.filter((e) => !ids.has(e.id))
    }));
    this.selectedIds.set([]);
    this.markDirty();
  }

  duplicateElement(): void {
    const slide = this.activeSlide();
    const ids = this.selectedIds();
    if (!slide || !ids.length) {
      return;
    }
    this.pushHistory();
    const copies: SlideElement[] = [];
    let z = this.nextZ();
    for (const id of ids) {
      const sel = slide.elements.find((e) => e.id === id);
      if (!sel) {
        continue;
      }
      copies.push({
        ...sel,
        id: newClientId('el'),
        x: sel.x + 24,
        y: sel.y + 24,
        z: z++,
        groupId: null,
        props: { ...sel.props } as SlideElement['props']
      });
    }
    if (!copies.length) {
      return;
    }
    this.updateActive((s) => ({
      ...s,
      elements: [...s.elements, ...copies]
    }));
    this.selectedIds.set(copies.map((c) => c.id));
    this.markDirty();
  }

  copyElement(): void {
    this.copySelectedElement();
  }

  pasteElement(): void {
    this.pasteElementClipboard();
  }

  nudgeSelected(dx: number, dy: number): void {
    const ids = new Set(this.selectedIds());
    if (!ids.size) {
      return;
    }
    this.pushHistory();
    this.updateActive((slide) => ({
      ...slide,
      elements: slide.elements.map((el) =>
        ids.has(el.id) ? { ...el, x: el.x + dx, y: el.y + dy } : el
      )
    }));
    this.markDirty();
  }

  align(kind: 'left' | 'center' | 'right' | 'top' | 'middle' | 'bottom'): void {
    const ids = this.selectedIds();
    const slide = this.activeSlide();
    if (!ids.length || !slide) {
      return;
    }
    const selected = ids
      .map((id) => slide.elements.find((e) => e.id === id))
      .filter((e): e is SlideElement => !!e);
    if (!selected.length) {
      return;
    }
    this.pushHistory();
    if (selected.length === 1) {
      const el = selected[0];
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
      this.patchElement(el.id, (e) => ({ ...e, x, y }));
      this.markDirty();
      return;
    }

    const minX = Math.min(...selected.map((e) => e.x));
    const minY = Math.min(...selected.map((e) => e.y));
    const maxX = Math.max(...selected.map((e) => e.x + e.w));
    const maxY = Math.max(...selected.map((e) => e.y + e.h));
    const idSet = new Set(ids);
    this.updateActive((s) => ({
      ...s,
      elements: s.elements.map((el) => {
        if (!idSet.has(el.id)) {
          return el;
        }
        let x = el.x;
        let y = el.y;
        if (kind === 'left') {
          x = minX;
        }
        if (kind === 'center') {
          x = Math.round(minX + (maxX - minX - el.w) / 2);
        }
        if (kind === 'right') {
          x = maxX - el.w;
        }
        if (kind === 'top') {
          y = minY;
        }
        if (kind === 'middle') {
          y = Math.round(minY + (maxY - minY - el.h) / 2);
        }
        if (kind === 'bottom') {
          y = maxY - el.h;
        }
        return { ...el, x, y };
      })
    }));
    this.markDirty();
  }

  /** Even spacing between 3+ selected objects. */
  distribute(axis: 'horizontal' | 'vertical'): void {
    const ids = this.selectedIds();
    const slide = this.activeSlide();
    if (ids.length < 3 || !slide) {
      return;
    }
    const selected = ids
      .map((id) => slide.elements.find((e) => e.id === id))
      .filter((e): e is SlideElement => !!e && !e.locked);
    if (selected.length < 3) {
      return;
    }
    this.pushHistory();
    const sorted =
      axis === 'horizontal'
        ? [...selected].sort((a, b) => a.x - b.x)
        : [...selected].sort((a, b) => a.y - b.y);
    const first = sorted[0];
    const last = sorted[sorted.length - 1];
    const totalSpan =
      axis === 'horizontal' ? last.x + last.w - first.x : last.y + last.h - first.y;
    const sizes = sorted.reduce((sum, e) => sum + (axis === 'horizontal' ? e.w : e.h), 0);
    const gap = (totalSpan - sizes) / (sorted.length - 1);
    let cursor = axis === 'horizontal' ? first.x : first.y;
    const pos = new Map<string, { x: number; y: number }>();
    for (const el of sorted) {
      if (axis === 'horizontal') {
        pos.set(el.id, { x: Math.round(cursor), y: el.y });
        cursor += el.w + gap;
      } else {
        pos.set(el.id, { x: el.x, y: Math.round(cursor) });
        cursor += el.h + gap;
      }
    }
    this.updateActive((s) => ({
      ...s,
      elements: s.elements.map((el) => {
        const p = pos.get(el.id);
        return p ? { ...el, x: p.x, y: p.y } : el;
      })
    }));
    this.markDirty();
  }

  toggleSlideHidden(): void {
    const slide = this.activeSlide();
    if (!slide || this.slides().length <= 1) {
      return;
    }
    const visibleCount = this.slides().filter((s) => !s.hidden).length;
    if (!slide.hidden && visibleCount <= 1) {
      this.error.set('Debe quedar al menos una diapositiva visible.');
      return;
    }
    this.pushHistory();
    this.updateActive((s) => ({ ...s, hidden: !s.hidden }));
    this.markDirty();
  }

  toggleCropEditing(): void {
    const sel = this.selected();
    if (!sel || sel.type !== 'image') {
      this.cropEditing.set(false);
      return;
    }
    this.cropEditing.update((v) => !v);
  }

  applyCropPreset(kind: 'center80' | 'top' | 'left' | 'reset'): void {
    if (kind === 'reset') {
      this.resetImageCrop();
      return;
    }
    if (kind === 'center80') {
      this.updateImageProp('crop', { x: 0.1, y: 0.1, w: 0.8, h: 0.8 });
      return;
    }
    if (kind === 'top') {
      this.updateImageProp('crop', { x: 0, y: 0, w: 1, h: 0.55 });
      return;
    }
    this.updateImageProp('crop', { x: 0, y: 0, w: 0.55, h: 1 });
  }

  onCropHandlePointerDown(ev: PointerEvent, edge: 'l' | 'r' | 't' | 'b'): void {
    ev.preventDefault();
    ev.stopPropagation();
    const sel = this.selected();
    if (!sel || sel.type !== 'image') {
      return;
    }
    this.pushHistory();
    const id = sel.id;
    const start = normalizeImageCrop((sel.props as ImageProps).crop);
    const startX = ev.clientX;
    const startY = ev.clientY;
    const z = this.zoom() || 1;
    const boxW = Math.max(sel.w, 1);
    const boxH = Math.max(sel.h, 1);
    const onMove = (moveEv: PointerEvent) => {
      const dx = (moveEv.clientX - startX) / z / boxW;
      const dy = (moveEv.clientY - startY) / z / boxH;
      let next = { ...start };
      if (edge === 'l') {
        const x = Math.min(start.x + start.w - 0.1, Math.max(0, start.x + dx));
        next = { ...next, x, w: start.w - (x - start.x) };
      } else if (edge === 'r') {
        next = { ...next, w: Math.min(1 - start.x, Math.max(0.1, start.w + dx)) };
      } else if (edge === 't') {
        const y = Math.min(start.y + start.h - 0.1, Math.max(0, start.y + dy));
        next = { ...next, y, h: start.h - (y - start.y) };
      } else {
        next = { ...next, h: Math.min(1 - start.y, Math.max(0.1, start.h + dy)) };
      }
      const crop = normalizeImageCrop(next);
      this.patchElement(id, (el) => {
        if (el.type !== 'image') {
          return el;
        }
        return { ...el, props: { ...(el.props as ImageProps), crop } };
      });
      this.markDirty();
    };
    const onUp = () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
    };
    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
  }

  updateTextProp<K extends keyof TextProps>(key: K, value: TextProps[K]): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.pushHistory();
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
    this.pushHistory();
    this.patchElement(id, (el) => {
      if (el.type !== 'shape') {
        return el;
      }
      return { ...el, props: { ...(el.props as ShapeProps), [key]: value } };
    });
    this.markDirty();
  }

  updateLineProp<K extends keyof LineProps>(key: K, value: LineProps[K]): void {
    const id = this.selectedId();
    if (!id) {
      return;
    }
    this.pushHistory();
    this.patchElement(id, (el) => {
      if (el.type !== 'line' && el.type !== 'arrow') {
        return el;
      }
      return { ...el, props: { ...(el.props as LineProps), [key]: value } };
    });
    this.markDirty();
  }

  updateBgColor(color: string): void {
    this.pushHistory();
    this.updateActive((s) => ({
      ...s,
      background: { ...s.background, type: 'solid', color }
    }));
    this.markDirty();
  }

  updateBgGradient(c1: string, c2: string): void {
    this.pushHistory();
    this.updateActive((s) => ({
      ...s,
      background: { type: 'gradient', color: c1, color2: c2 }
    }));
    this.markDirty();
  }

  clearBgImage(): void {
    this.pushHistory();
    this.updateActive((s) => ({
      ...s,
      background: { type: 'solid', color: s.background.color || '#F7F9FC' }
    }));
    this.markDirty();
  }

  updateBgImageUrl(url: string): void {
    this.pushHistory();
    this.updateActive((s) => ({
      ...s,
      background: { type: 'image', color: s.background.color || '#F7F9FC', imageUrl: url }
    }));
    this.markDirty();
  }

  triggerBgImage(): void {
    this.bgImageInput?.nativeElement.click();
  }

  onBgImageSelected(ev: Event): void {
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
    this.imageUploading.set(true);
    this.api.upload(file).subscribe({
      next: (res) => {
        this.imageUploading.set(false);
        this.updateBgImageUrl(res.url);
      },
      error: (err) => {
        this.imageUploading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  changeLayer(direction: 'forward' | 'back' | 'front' | 'backmost'): void {
    const id = this.selectedId();
    const slide = this.activeSlide();
    if (!id || !slide) {
      return;
    }
    const sorted = [...slide.elements].sort((a, b) => a.z - b.z);
    const idx = sorted.findIndex((e) => e.id === id);
    if (idx < 0) {
      return;
    }
    this.pushHistory();
    const swap = (a: number, b: number) => {
      const za = sorted[a].z;
      sorted[a] = { ...sorted[a], z: sorted[b].z };
      sorted[b] = { ...sorted[b], z: za };
    };
    if (direction === 'forward' && idx < sorted.length - 1) {
      swap(idx, idx + 1);
    } else if (direction === 'back' && idx > 0) {
      swap(idx, idx - 1);
    } else if (direction === 'front') {
      const maxZ = Math.max(...sorted.map((e) => e.z));
      sorted[idx] = { ...sorted[idx], z: maxZ + 1 };
    } else if (direction === 'backmost') {
      const minZ = Math.min(...sorted.map((e) => e.z));
      sorted[idx] = { ...sorted[idx], z: minZ - 1 };
    }
    const byId = new Map(sorted.map((e) => [e.id, e]));
    this.updateActive((s) => ({
      ...s,
      elements: s.elements.map((e) => byId.get(e.id) ?? e)
    }));
    this.markDirty();
  }

  toggleBullets(): void {
    const sel = this.selected();
    if (!sel || sel.type !== 'text') {
      return;
    }
    const props = sel.props as TextProps;
    const lines = props.text.split('\n');
    const hasBullets = lines.some((l) => l.trim().startsWith('•'));
    const next = lines
      .map((line) => {
        const trimmed = line.trim();
        if (!trimmed) {
          return line;
        }
        if (hasBullets) {
          return line.replace(/^\s*•\s?/, '');
        }
        return line.startsWith('•') ? line : `• ${line.trimStart()}`;
      })
      .join('\n');
    this.updateTextProp('text', next);
  }

  toggleTextStyle(prop: 'italic' | 'underline'): void {
    const sel = this.selected();
    if (!sel || sel.type !== 'text') {
      return;
    }
    const props = sel.props as TextProps;
    this.updateTextProp(prop, !props[prop]);
  }

  bumpFontSize(delta: number): void {
    const sel = this.selectedText();
    if (!sel) {
      return;
    }
    const size = (sel.props as TextProps).fontSize || 20;
    this.updateTextProp('fontSize', Math.max(10, Math.min(120, size + delta)));
  }

  updateDescription(value: string): void {
    this.description.set(value);
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

  async present(): Promise<void> {
    if (!(await this.flushSave('presentar'))) {
      return;
    }
    this.skipUnload = true;
    void this.router.navigate(['/teacher/presentations', this.presentationId(), 'present']);
  }

  async presentLive(): Promise<void> {
    if (!(await this.flushSave('abrir en vivo'))) {
      return;
    }
    this.skipUnload = true;
    void this.router.navigate(['/teacher/live'], {
      queryParams: {
        presentationId: this.presentationId(),
        title: this.title().trim() || undefined,
        autoCreate: 1
      }
    });
  }

  async exportDeck(format: 'xlsx' | 'docx' | 'pptx'): Promise<void> {
    const id = this.presentationId();
    if (!id) return;
    this.closeFloatingMenus();
    if (!(await this.flushSave('exportar'))) {
      return;
    }
    this.api.exportFile(id, format).subscribe({
      next: (blob) => {
        const name = (this.title().trim() || 'presentacion').replace(/[<>:"/\\|?*]+/g, '-');
        downloadBlob(blob, `${name}.${format}`);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  async exportPdfPrint(): Promise<void> {
    this.closeFloatingMenus();
    if (!(await this.flushSave('exportar PDF'))) {
      return;
    }
    try {
      openPresentationPrintPdf(
        this.title().trim() || 'Presentación',
        this.slides().filter((s) => !s.hidden)
      );
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo abrir la impresión a PDF.');
    }
  }

  async exportActiveSlidePng(): Promise<void> {
    const slide = this.activeSlide();
    if (!slide || this.exportingPng()) {
      return;
    }
    this.closeFloatingMenus();
    if (!(await this.flushSave('exportar PNG'))) {
      return;
    }
    this.exportingPng.set(true);
    try {
      const blob = await renderSlideToPngBlob(slide);
      const name = (this.title().trim() || 'diapositiva').replace(/[<>:"/\\|?*]+/g, '-');
      downloadBlob(blob, `${name}-diapositiva-${this.activeIndex() + 1}.png`);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo generar el PNG.');
    } finally {
      this.exportingPng.set(false);
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
    return shapeClipPath(shape);
  }

  private pushElement(el: SlideElement): void {
    this.updateActive((slide) => ({
      ...slide,
      elements: [...slide.elements, el]
    }));
    this.setSelectedId(el.id);
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
        this.slides.set(draft.slides.map(unlockImportedPhotoSlide));
        this.markDirty();
      } else {
        this.clearLocalDraft(id);
      }
    } catch {
      /* ignore */
    }
  }
}
