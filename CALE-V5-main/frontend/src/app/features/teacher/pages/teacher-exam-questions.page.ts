import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, Subscription, debounceTime } from 'rxjs';
import { mapApiError } from '../../../core/http/map-api-error';
import { env } from '../../../core/config/env';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiImagePickerComponent } from '../../../shared/ui/ui-image-picker.component';
import { ExamDto } from '../../student/api/exam.api';
import {
  QuestionDetailDto,
  QuestionListDto,
  TeacherApi
} from '../api/teacher.api';

interface OptionDraft {
  text: string;
  isCorrect: boolean;
  imageUrl: string;
}

type FilterMode = 'all' | 'no-key' | 'no-image' | 'inactive';
type SaveState = 'idle' | 'dirty' | 'saving' | 'saved' | 'error';

@Component({
  selector: 'app-teacher-exam-questions-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiImagePickerComponent
  ],
  templateUrl: './teacher-exam-questions.page.html',
  styleUrl: './teacher-exam-questions.page.css'
})
export class TeacherExamQuestionsPage implements OnInit, OnDestroy {
  private readonly api = inject(TeacherApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly autosave$ = new Subject<void>();
  private autosaveSub?: Subscription;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly saveState = signal<SaveState>('idle');
  readonly uploading = signal<string | null>(null);
  readonly exam = signal<ExamDto | null>(null);
  readonly items = signal<QuestionListDto[]>([]);
  readonly selectedId = signal<number | null>(null);
  readonly filter = signal<FilterMode>('all');
  readonly search = signal('');
  readonly blocks = signal<Array<{ id: number; name: string }>>([]);
  readonly isNew = signal(false);

  examId = 0;
  bankId = 0;
  blockId = 0;
  questionId: number | null = null;
  text = '';
  type = 'Seleccion multiple';
  topic = '';
  explanation = '';
  imageUrl = '';
  isActive = true;
  options: OptionDraft[] = this.blankOptions();
  private lastType = 'Seleccion multiple';
  private suppressAutosave = false;

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const mode = this.filter();
    return this.items().filter((item) => {
      if (q && !item.text.toLowerCase().includes(q) && !(item.topic ?? '').toLowerCase().includes(q)) {
        return false;
      }
      if (mode === 'no-key' && item.hasCorrectAnswer) {
        return false;
      }
      if (mode === 'no-image' && item.imageUrl) {
        return false;
      }
      if (mode === 'inactive' && item.isActive) {
        return false;
      }
      return true;
    });
  });

  readonly missingKeyCount = computed(
    () => this.items().filter((i) => !i.hasCorrectAnswer).length
  );
  readonly missingImageCount = computed(
    () => this.items().filter((i) => !i.imageUrl).length
  );
  readonly inactiveCount = computed(
    () => this.items().filter((i) => !i.isActive).length
  );

  readonly selectedIndex = computed(() => {
    const id = this.selectedId();
    if (id == null) {
      return -1;
    }
    return this.filtered().findIndex((i) => i.id === id);
  });

  get isTrueFalse(): boolean {
    return this.type === 'Verdadero/Falso';
  }

  get saveLabel(): string {
    switch (this.saveState()) {
      case 'dirty':
        return 'Sin guardar';
      case 'saving':
        return 'Guardando…';
      case 'saved':
        return 'Guardado';
      case 'error':
        return 'Error al guardar';
      default:
        return 'Listo';
    }
  }

  ngOnInit(): void {
    this.autosaveSub = this.autosave$.pipe(debounceTime(900)).subscribe(() => this.save(true));
    this.api.blocks().subscribe({
      next: (blocks) => {
        this.blocks.set(blocks);
        if (!this.blockId && blocks[0]) {
          this.blockId = blocks[0].id;
        }
      }
    });
    this.route.paramMap.subscribe((params) => {
      this.examId = Number(params.get('examId') || 0);
      this.bootstrap();
    });
  }

  ngOnDestroy(): void {
    this.autosaveSub?.unsubscribe();
  }

  mediaUrl(path?: string | null): string | null {
    if (!path) {
      return null;
    }
    if (path.startsWith('http')) {
      return path;
    }
    const base = env.apiUrl.replace(/\/$/, '');
    return `${base}${path.startsWith('/') ? path : '/' + path}`;
  }

  letter(index: number): string {
    return String.fromCharCode(65 + index);
  }

  setFilter(mode: FilterMode): void {
    this.filter.set(mode);
  }

  onSearch(value: string): void {
    this.search.set(value);
  }

  selectQuestion(id: number): void {
    if (this.saveState() === 'dirty') {
      this.save(true);
    }
    this.loadQuestion(id);
  }

  startNew(): void {
    if (!this.bankId) {
      this.error.set('Este examen no tiene banco. Impórtalo o asígnale un banco primero.');
      return;
    }
    this.suppressAutosave = true;
    this.isNew.set(true);
    this.selectedId.set(null);
    this.questionId = null;
    this.text = '';
    this.type = 'Seleccion multiple';
    this.lastType = this.type;
    this.topic = '';
    this.explanation = '';
    this.imageUrl = '';
    this.isActive = true;
    this.options = this.blankOptions();
    this.saveState.set('dirty');
    this.suppressAutosave = false;
  }

  duplicateCurrent(): void {
    if (!this.text.trim()) {
      return;
    }
    this.suppressAutosave = true;
    this.isNew.set(true);
    this.selectedId.set(null);
    this.questionId = null;
    this.saveState.set('dirty');
    this.suppressAutosave = false;
    this.save(false);
  }

  prev(): void {
    const idx = this.selectedIndex();
    const list = this.filtered();
    if (idx > 0) {
      this.selectQuestion(list[idx - 1].id);
    }
  }

  next(): void {
    const idx = this.selectedIndex();
    const list = this.filtered();
    if (idx >= 0 && idx < list.length - 1) {
      this.selectQuestion(list[idx + 1].id);
    }
  }

  markDirty(): void {
    if (this.suppressAutosave) {
      return;
    }
    this.saveState.set('dirty');
    this.autosave$.next();
  }

  onTypeChange(): void {
    if (this.type === this.lastType) {
      return;
    }
    this.lastType = this.type;
    if (this.isTrueFalse) {
      this.options = [
        { text: 'Verdadero', isCorrect: true, imageUrl: '' },
        { text: 'Falso', isCorrect: false, imageUrl: '' }
      ];
    }
    this.markDirty();
  }

  markCorrect(index: number): void {
    this.options = this.options.map((o, i) => ({ ...o, isCorrect: i === index }));
    this.markDirty();
  }

  addOption(): void {
    if (this.isTrueFalse) {
      return;
    }
    this.options = [...this.options, { text: '', isCorrect: false, imageUrl: '' }];
    this.markDirty();
  }

  removeOption(index: number): void {
    if (this.options.length <= 2) {
      this.error.set('Deja al menos dos respuestas.');
      return;
    }
    const wasCorrect = this.options[index].isCorrect;
    this.options = this.options.filter((_, i) => i !== index);
    if (wasCorrect) {
      this.markCorrect(0);
    } else {
      this.markDirty();
    }
  }

  uploadQuestionImage(file: File): void {
    this.upload(file, (url) => {
      this.imageUrl = url;
      this.markDirty();
    }, 'question');
  }

  uploadOptionImage(index: number, file: File): void {
    this.upload(file, (url) => {
      this.options = this.options.map((o, i) => (i === index ? { ...o, imageUrl: url } : o));
      this.markDirty();
    }, `opt-${index}`);
  }

  clearQuestionImage(): void {
    this.imageUrl = '';
    this.markDirty();
  }

  clearOptionImage(index: number): void {
    this.options = this.options.map((o, i) => (i === index ? { ...o, imageUrl: '' } : o));
    this.markDirty();
  }

  deactivateCurrent(): void {
    if (!this.questionId) {
      return;
    }
    if (!confirm('¿Desactivar esta pregunta? Dejará de salir en el examen.')) {
      return;
    }
    this.isActive = false;
    this.save(false);
  }

  save(silent = false): void {
    this.error.set(null);
    if (!this.bankId || !this.blockId) {
      if (!silent) {
        this.error.set('Falta banco o bloque de contenido.');
      }
      return;
    }
    if (!this.text.trim()) {
      if (!silent) {
        this.error.set('Escribe el enunciado.');
      }
      return;
    }
    if (this.options.length < 2) {
      if (!silent) {
        this.error.set('Agrega al menos dos respuestas.');
      }
      return;
    }
    if (this.options.filter((o) => o.isCorrect).length !== 1) {
      if (!silent) {
        this.error.set('Marca exactamente una respuesta correcta.');
      }
      return;
    }
    if (this.options.some((o) => !o.text.trim() && !o.imageUrl)) {
      if (!silent) {
        this.error.set('Cada respuesta necesita texto o imagen.');
      }
      return;
    }

    const body = {
      bankId: this.bankId,
      blockId: this.blockId,
      text: this.text.trim(),
      type: this.type,
      topic: this.topic.trim() || null,
      imageUrl: this.imageUrl || null,
      explanation: this.explanation.trim() || null,
      isActive: this.isActive,
      options: this.options.map((o) => ({
        text: o.text.trim(),
        isCorrect: o.isCorrect,
        imageUrl: o.imageUrl || null
      }))
    };

    this.saveState.set('saving');
    this.api.saveQuestion(body, this.questionId ?? undefined).subscribe({
      next: (res) => {
        const createdId = this.questionId ?? (res && 'id' in res ? Number(res.id) : null);
        this.saveState.set('saved');
        this.isNew.set(false);
        this.reloadList(createdId ?? this.questionId ?? undefined);
        if (!silent && createdId) {
          this.loadQuestion(createdId);
        }
      },
      error: (err) => {
        this.saveState.set('error');
        this.error.set(mapApiError(err));
      }
    });
  }

  publishExam(): void {
    const exam = this.exam();
    if (!exam) {
      return;
    }
    this.api.publishExam(exam.id, true).subscribe({
      next: () => {
        this.exam.set({ ...exam, published: true });
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  openReview(): void {
    const exam = this.exam();
    if (!exam?.bankId) {
      return;
    }
    void this.router.navigate(['/teacher/exam-review'], {
      queryParams: { bankId: exam.bankId, examId: exam.id, name: exam.name }
    });
  }

  private bootstrap(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.exams().subscribe({
      next: (exams) => {
        const exam = exams.find((e) => e.id === this.examId) ?? null;
        if (!exam) {
          this.loading.set(false);
          this.error.set('Examen no encontrado.');
          return;
        }
        this.exam.set(exam);
        this.bankId = exam.bankId ?? 0;
        if (!this.bankId) {
          this.loading.set(false);
          this.error.set('Este examen no tiene banco de preguntas asignado.');
          return;
        }
        this.reloadList();
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private reloadList(selectId?: number): void {
    this.api.questions(1, 200, this.bankId).subscribe({
      next: (page) => {
        this.items.set(page.items);
        this.loading.set(false);
        const pick = selectId ?? this.selectedId() ?? page.items[0]?.id;
        if (pick) {
          this.loadQuestion(pick);
        } else if (!this.isNew()) {
          this.startNew();
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private loadQuestion(id: number): void {
    this.suppressAutosave = true;
    this.isNew.set(false);
    this.selectedId.set(id);
    this.api.question(id).subscribe({
      next: (q) => this.applyDetail(q),
      error: (err) => {
        this.suppressAutosave = false;
        this.error.set(mapApiError(err));
      }
    });
  }

  private applyDetail(q: QuestionDetailDto): void {
    this.questionId = q.id;
    this.bankId = q.bankId;
    this.blockId = q.blockId;
    this.text = q.text;
    this.type = q.type;
    this.lastType = q.type;
    this.topic = q.topic ?? '';
    this.explanation = q.explanation ?? '';
    this.imageUrl = q.imageUrl ?? '';
    this.isActive = q.isActive;
    this.options = q.options.map((o) => ({
      text: o.text,
      isCorrect: o.isCorrect,
      imageUrl: o.imageUrl ?? ''
    }));
    if (this.options.length < 2) {
      this.options = this.blankOptions();
    }
    this.saveState.set('idle');
    this.suppressAutosave = false;
  }

  private blankOptions(): OptionDraft[] {
    return [
      { text: '', isCorrect: true, imageUrl: '' },
      { text: '', isCorrect: false, imageUrl: '' },
      { text: '', isCorrect: false, imageUrl: '' },
      { text: '', isCorrect: false, imageUrl: '' }
    ];
  }

  private upload(file: File, apply: (url: string) => void, key: string): void {
    this.error.set(null);
    this.uploading.set(key);
    this.api.upload(file).subscribe({
      next: (res) => {
        apply(res.url);
        this.uploading.set(null);
      },
      error: (err) => {
        this.uploading.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }
}
