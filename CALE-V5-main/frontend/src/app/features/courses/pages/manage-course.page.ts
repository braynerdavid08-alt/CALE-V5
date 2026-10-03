import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { mapApiError } from '../../../core/http/map-api-error';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import {
  BlockType,
  CatalogSign,
  ClassifyBlock,
  CourseManageDetail,
  FillBlankBlock,
  HotspotBlock,
  OrderBlock,
  ScenarioBlock,
  TrueFalseBlock,
  CourseProgressReport,
  CoursesManageApi,
  FlipcardsBlock,
  LessonBlock,
  LessonManage,
  MatchBlock,
  MediaBlock,
  QuizBlock,
  SignsBlock,
  TextBlock
} from '../api/courses.api';
import { LessonBlocksComponent } from '../components/lesson-blocks.component';

interface LessonDraft {
  title: string;
  summary: string;
  estimatedMinutes: number;
  content: LessonBlock[];
}

interface CourseMeta {
  title: string;
  description: string;
  category: string;
  coverUrl: string;
}

type Tab = 'edit' | 'preview' | 'progress';

export const BLOCK_LABELS: Record<BlockType, { label: string; icon: string; hint: string }> = {
  text: { label: 'Texto', icon: '📝', hint: 'Explicación con título opcional. Deja una línea en blanco para separar párrafos.' },
  tip: { label: 'Consejo', icon: '💡', hint: 'Recuadro amarillo para destacar una idea importante.' },
  image: { label: 'Imagen', icon: '🖼️', hint: 'Sube una foto o pega un enlace.' },
  video: { label: 'Video', icon: '🎬', hint: 'Sube un video corto o pega un enlace de YouTube.' },
  audio: { label: 'Audio', icon: '🔊', hint: 'Sube un audio (mp3, m4a, ogg, wav).' },
  signs: { label: 'Señales', icon: '🚦', hint: 'Elige señales del catálogo y agrega una nota corta.' },
  quiz: { label: 'Pregunta', icon: '❓', hint: 'Pregunta de selección; marca la opción correcta.' },
  truefalse: { label: 'Verdadero o falso', icon: '✅', hint: 'Una afirmación corta; marca si es verdadera o falsa.' },
  scenario: { label: 'Situación de manejo', icon: '🚗', hint: 'Describe una situación real; cada decisión muestra lo que pasaría. Marca la mejor.' },
  order: { label: 'Ordenar pasos', icon: '🔢', hint: 'Escribe los pasos en el orden correcto; al estudiante le salen mezclados.' },
  fillblank: { label: 'Completar la frase', icon: '✏️', hint: 'Pon cada respuesta entre doble corchete: «El límite en zona escolar es [[30]] km/h». Para aceptar varias respuestas: [[50|cincuenta]].' },
  classify: { label: 'Clasificar', icon: '🗂️', hint: 'Crea 2 a 4 grupos y elige a cuál pertenece cada elemento.' },
  hotspot: { label: 'Tocar la imagen', icon: '👆', hint: 'Sube una imagen y toca sobre ella para marcar los puntos que el estudiante debe encontrar.' },
  flipcards: { label: 'Tarjetas', icon: '🃏', hint: 'Tarjetas que se voltean: frente y respuesta.' },
  match: { label: 'Unir parejas', icon: '🔗', hint: 'El estudiante une cada elemento con su pareja.' }
};

@Component({
  selector: 'app-manage-course-page',
  standalone: true,
  imports: [DatePipe, NgTemplateOutlet, FormsModule, RouterLink, UiButtonComponent, UiLoadingComponent, LessonBlocksComponent],
  templateUrl: './manage-course.page.html',
  styleUrls: ['./courses.css', './manage-course.page.css']
})
export class ManageCoursePage implements OnInit {
  private readonly api = inject(CoursesManageApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly base: string = this.route.snapshot.data['base'] ?? '/teacher/cursos';
  readonly blockTypes = Object.keys(BLOCK_LABELS) as BlockType[];
  readonly labels = BLOCK_LABELS;
  readonly letters = ['A', 'B', 'C', 'D', 'E', 'F'];

  readonly loading = signal(true);
  readonly busy = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly course = signal<CourseManageDetail | null>(null);
  readonly selectedId = signal<number | null>(null);
  readonly tab = signal<Tab>('edit');
  readonly progress = signal<CourseProgressReport | null>(null);
  readonly selected = computed(() => this.course()?.lessons.find((l) => l.id === this.selectedId()) ?? null);

  meta: CourseMeta = { title: '', description: '', category: '', coverUrl: '' };
  private metaSnapshot = '';
  draft: LessonDraft | null = null;
  private draftSnapshot = '';
  previewBlocks: LessonBlock[] = [];

  readonly signs = signal<CatalogSign[]>([]);
  readonly pickerOpen = signal(false);
  pickerSearch = '';
  pickerFamily = '';
  private pickerTarget: ((s: CatalogSign) => void) | null = null;
  private pickerMulti = false;

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.api.get(id).subscribe({
      next: (c) => {
        this.setCourse(c);
        this.selectLesson(c.lessons[0]?.id ?? null, true);
        if (!c.canEdit) this.tab.set('preview');
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  url(path?: string | null): string {
    return resolveMediaUrl(path);
  }

  // ── Course ──────────────────────────────────────────────────────────

  metaDirty(): boolean {
    return JSON.stringify(this.meta) !== this.metaSnapshot;
  }

  lessonDirty(): boolean {
    return !!this.draft && JSON.stringify(this.draft) !== this.draftSnapshot;
  }

  saveMeta(publish?: boolean): void {
    const c = this.course();
    if (!c) return;
    const isPublished = publish ?? c.isPublished;
    this.run('meta', this.api.update(c.id, { ...this.meta, isPublished }), () => {
      this.course.set({ ...c, ...this.meta, isPublished });
      this.metaSnapshot = JSON.stringify(this.meta);
      this.flash(publish === undefined ? 'Datos del curso guardados.' : isPublished
        ? 'Curso publicado: tus estudiantes ya lo pueden ver.'
        : 'El curso quedó como borrador y los estudiantes ya no lo ven.');
    });
  }

  togglePublish(): void {
    const c = this.course();
    if (!c) return;
    if (this.lessonDirty()) {
      this.saveLesson(() => this.saveMeta(!c.isPublished));
    } else {
      this.saveMeta(!c.isPublished);
    }
  }

  deleteCourse(): void {
    const c = this.course();
    if (!c || !confirm(`¿Eliminar el curso «${c.title}»? Los estudiantes dejarán de verlo.`)) return;
    this.run('delete', this.api.remove(c.id), () => this.router.navigate([this.base]));
  }

  duplicate(): void {
    const c = this.course();
    if (!c) return;
    this.run('duplicate', this.api.duplicate(c.id), (copy) => {
      this.router.navigate([this.base, copy.id]).then(() => {
        this.setCourse(copy);
        this.selectLesson(copy.lessons[0]?.id ?? null, true);
        this.tab.set('edit');
        this.flash('Copia creada. Ya puedes editarla.');
      });
    });
  }

  uploadCover(event: Event): void {
    this.upload(event, (url) => (this.meta.coverUrl = url));
  }

  pickCover(): void {
    this.openPicker((s) => (this.meta.coverUrl = s.imageUrl));
  }

  // ── Lessons ─────────────────────────────────────────────────────────

  selectLesson(id: number | null, force = false): void {
    if (!force && id === this.selectedId()) return;
    if (!force && this.lessonDirty() && !confirm('Tienes cambios sin guardar en esta lección. ¿Salir sin guardar?')) return;
    this.selectedId.set(id);
    const lesson = this.course()?.lessons.find((l) => l.id === id);
    this.draft = lesson
      ? {
          title: lesson.title,
          summary: lesson.summary ?? '',
          estimatedMinutes: lesson.estimatedMinutes,
          content: structuredClone(lesson.content ?? [])
        }
      : null;
    this.draftSnapshot = JSON.stringify(this.draft);
    this.previewBlocks = lesson ? structuredClone(lesson.content ?? []) : [];
  }

  addLesson(): void {
    const c = this.course();
    if (!c) return;
    if (this.lessonDirty() && !confirm('Tienes cambios sin guardar en esta lección. ¿Continuar sin guardar?')) return;
    this.run('lesson-add', this.api.addLesson(c.id, ''), (lesson) => {
      this.course.set({ ...c, lessons: [...c.lessons, lesson] });
      this.selectLesson(lesson.id, true);
      this.tab.set('edit');
    });
  }

  saveLesson(after?: () => void): void {
    const lesson = this.selected();
    if (!lesson || !this.draft) return;
    const body = { ...this.draft, content: this.draft.content };
    this.run('lesson', this.api.updateLesson(lesson.id, body), (saved) => {
      const c = this.course()!;
      this.course.set({ ...c, lessons: c.lessons.map((l) => (l.id === saved.id ? saved : l)) });
      this.selectLesson(saved.id, true);
      if (after) after(); else this.flash('Lección guardada.');
    });
  }

  deleteLesson(): void {
    const lesson = this.selected();
    const c = this.course();
    if (!lesson || !c || !confirm(`¿Eliminar la lección «${lesson.title}»?`)) return;
    this.run('lesson-delete', this.api.removeLesson(lesson.id), () => {
      const lessons = c.lessons.filter((l) => l.id !== lesson.id).map((l, i) => ({ ...l, position: i }));
      this.course.set({ ...c, lessons, isPublished: lessons.length ? c.isPublished : false });
      this.selectLesson(lessons[0]?.id ?? null, true);
    });
  }

  moveLesson(lesson: LessonManage, delta: number): void {
    const c = this.course();
    if (!c) return;
    const list = [...c.lessons];
    const from = list.findIndex((l) => l.id === lesson.id);
    const to = from + delta;
    if (to < 0 || to >= list.length) return;
    [list[from], list[to]] = [list[to], list[from]];
    this.run('reorder', this.api.reorder(c.id, list.map((l) => l.id)), () => {
      this.course.set({ ...c, lessons: list.map((l, i) => ({ ...l, position: i })) });
    });
  }

  // ── Blocks ──────────────────────────────────────────────────────────

  addBlock(type: BlockType): void {
    if (!this.draft) return;
    this.draft.content.push(this.newBlock(type));
    setTimeout(() => {
      const items = document.querySelectorAll('.block-card');
      items[items.length - 1]?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
  }

  moveBlock(i: number, delta: number): void {
    const list = this.draft?.content;
    const to = i + delta;
    if (!list || to < 0 || to >= list.length) return;
    [list[i], list[to]] = [list[to], list[i]];
  }

  removeBlock(i: number): void {
    if (!this.draft || !confirm('¿Quitar este bloque?')) return;
    this.draft.content.splice(i, 1);
  }

  asText(b: LessonBlock): TextBlock { return b as TextBlock; }
  asMedia(b: LessonBlock): MediaBlock { return b as MediaBlock; }
  asSigns(b: LessonBlock): SignsBlock { return b as SignsBlock; }
  asQuiz(b: LessonBlock): QuizBlock { return b as QuizBlock; }
  asCards(b: LessonBlock): FlipcardsBlock { return b as FlipcardsBlock; }
  asMatch(b: LessonBlock): MatchBlock { return b as MatchBlock; }
  asTf(b: LessonBlock): TrueFalseBlock { return b as TrueFalseBlock; }
  asScenario(b: LessonBlock): ScenarioBlock { return b as ScenarioBlock; }
  asOrder(b: LessonBlock): OrderBlock { return b as OrderBlock; }
  asFill(b: LessonBlock): FillBlankBlock { return b as FillBlankBlock; }
  asClassify(b: LessonBlock): ClassifyBlock { return b as ClassifyBlock; }
  asHotspot(b: LessonBlock): HotspotBlock { return b as HotspotBlock; }

  /** Moves an item inside any list of a block (steps, options, cards…). */
  moveItem<T>(list: T[], k: number, delta: number): void {
    const to = k + delta;
    if (to < 0 || to >= list.length) return;
    [list[k], list[to]] = [list[to], list[k]];
  }

  setBest(s: ScenarioBlock, k: number): void {
    s.choices.forEach((c, i) => (c.best = i === k));
  }

  blankCount(f: FillBlankBlock): number {
    return ((f.text ?? '').match(/\[\[(.+?)\]\]/g) ?? []).length;
  }

  removeGroup(c: ClassifyBlock, g: number): void {
    if (c.groups.length <= 2) return;
    c.groups.splice(g, 1);
    c.items.forEach((it) => {
      if ((it.group ?? 0) === g) it.group = 0;
      else if ((it.group ?? 0) > g) it.group = (it.group ?? 0) - 1;
    });
  }

  addSpot(h: HotspotBlock, event: MouseEvent): void {
    if (h.spots.length >= 8) {
      this.flash('Máximo 8 puntos por imagen.');
      return;
    }
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const x = Math.round(((event.clientX - rect.left) / rect.width) * 1000) / 10;
    const y = Math.round(((event.clientY - rect.top) / rect.height) * 1000) / 10;
    h.spots.push({ x, y, label: '', note: '' });
  }

  mediaAccept(type: BlockType): string {
    return type === 'video' ? 'video/*' : type === 'audio' ? 'audio/*' : 'image/*';
  }

  uploadInto(event: Event, target: { url?: string | null; imageUrl?: string | null }, key: 'url' | 'imageUrl'): void {
    this.upload(event, (url) => (target[key] = url));
  }

  pickSignInto(target: { imageUrl?: string | null }): void {
    this.openPicker((s) => (target.imageUrl = s.imageUrl));
  }

  addSigns(block: SignsBlock): void {
    this.openPicker((s) => {
      if (block.items.length >= 24 || block.items.some((x) => x.code === s.code)) return;
      block.items.push({ code: s.code, name: this.niceName(s.name), note: '' });
    }, true);
  }

  addOption(q: QuizBlock): void {
    if (q.options.length < 6) q.options.push('');
  }

  removeOption(q: QuizBlock, o: number): void {
    if (q.options.length <= 2) return;
    q.options.splice(o, 1);
    if ((q.correct ?? 0) >= q.options.length) q.correct = 0;
    else if ((q.correct ?? 0) > o) q.correct = (q.correct ?? 0) - 1;
  }

  trackIndex(i: number): number {
    return i;
  }

  // ── Preview & progress ──────────────────────────────────────────────

  setTab(tab: Tab): void {
    this.tab.set(tab);
    if (tab === 'preview' && this.draft) {
      this.previewBlocks = structuredClone(this.draft.content);
    }
    if (tab === 'progress' && !this.progress()) {
      const c = this.course();
      if (c) this.run('progress', this.api.progress(c.id), (p) => this.progress.set(p));
    }
  }

  // ── Signs picker ────────────────────────────────────────────────────

  families = computed(() => [...new Set(this.signs().map((s) => s.family))]);

  filteredSigns(): CatalogSign[] {
    const q = this.normalize(this.pickerSearch);
    return this.signs().filter((s) =>
      (!this.pickerFamily || s.family === this.pickerFamily)
      && (!q || this.normalize(`${s.code} ${s.name}`).includes(q)));
  }

  choose(s: CatalogSign): void {
    this.pickerTarget?.(s);
    if (!this.pickerMulti) this.closePicker();
    else this.flash(`Agregada: ${this.niceName(s.name)}`);
  }

  closePicker(): void {
    this.pickerOpen.set(false);
    this.pickerTarget = null;
  }

  niceName(name: string): string {
    const lower = name.toLocaleLowerCase('es');
    return lower.charAt(0).toLocaleUpperCase('es') + lower.slice(1);
  }

  // ── Helpers ─────────────────────────────────────────────────────────

  private openPicker(target: (s: CatalogSign) => void, multi = false): void {
    this.pickerTarget = target;
    this.pickerMulti = multi;
    this.pickerSearch = '';
    this.pickerOpen.set(true);
    if (!this.signs().length) {
      this.api.signs().subscribe({
        next: (list) => this.signs.set(list),
        error: (err) => this.error.set(mapApiError(err))
      });
    }
  }

  private upload(event: Event, done: (url: string) => void): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.run('upload', this.api.upload(file), (res) => done(res.url));
  }

  private setCourse(c: CourseManageDetail): void {
    this.course.set(c);
    this.meta = {
      title: c.title,
      description: c.description ?? '',
      category: c.category,
      coverUrl: c.coverUrl ?? ''
    };
    this.metaSnapshot = JSON.stringify(this.meta);
    this.progress.set(null);
  }

  private newBlock(type: BlockType): LessonBlock {
    switch (type) {
      case 'text': return { type, title: '', body: '' };
      case 'tip': return { type, body: '' };
      case 'image':
      case 'video':
      case 'audio': return { type, url: '', caption: '' };
      case 'signs': return { type, title: '', items: [] };
      case 'quiz': return { type, question: '', imageUrl: null, options: ['', ''], correct: 0, explanation: '' };
      case 'flipcards': return { type, title: '', cards: [{ front: '', back: '', imageUrl: null }] };
      case 'match': return {
        type,
        instructions: '',
        pairs: [{ left: '', imageUrl: null, right: '' }, { left: '', imageUrl: null, right: '' }]
      };
      case 'truefalse': return { type, statement: '', imageUrl: null, answer: true, explanation: '' };
      case 'scenario': return {
        type,
        situation: '',
        imageUrl: null,
        choices: [{ text: '', outcome: '', best: true }, { text: '', outcome: '', best: false }]
      };
      case 'order': return { type, instructions: '', steps: ['', '', ''], explanation: '' };
      case 'fillblank': return { type, text: '', distractors: [], explanation: '' };
      case 'classify': return {
        type,
        instructions: '',
        groups: ['', ''],
        items: [{ text: '', imageUrl: null, group: 0 }, { text: '', imageUrl: null, group: 1 }],
        explanation: ''
      };
      case 'hotspot': return { type, instructions: '', imageUrl: '', spots: [] };
    }
  }

  private normalize(v: string): string {
    return (v ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
  }

  private flash(msg: string): void {
    this.notice.set(msg);
    setTimeout(() => {
      if (this.notice() === msg) this.notice.set(null);
    }, 3500);
  }

  private run<T>(key: string, obs: Observable<T>, next: (value: T) => void): void {
    this.busy.set(key);
    this.error.set(null);
    obs.subscribe({
      next: (value) => {
        this.busy.set(null);
        next(value);
      },
      error: (err) => {
        this.busy.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }
}
