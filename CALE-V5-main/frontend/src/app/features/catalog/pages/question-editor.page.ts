import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiImagePickerComponent } from '../../../shared/ui/ui-image-picker.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { TeacherApi } from '../../teacher/api/teacher.api';

interface OptionDraft {
  text: string;
  isCorrect: boolean;
  imageUrl: string;
}

@Component({
  selector: 'app-question-editor-page',
  standalone: true,
  imports: [
    FormsModule,
    UiButtonComponent,
    UiCardComponent,
    UiErrorComponent,
    UiImagePickerComponent,
    UiPageHeaderComponent,
    UiSuccessComponent
  ],
  templateUrl: './question-editor.page.html',
  styleUrl: './question-editor.page.css'
})
export class QuestionEditorPage implements OnInit {
  private readonly api = inject(TeacherApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly saving = signal(false);
  readonly uploading = signal<string | null>(null);
  readonly banks = signal<Array<{ id: number; name: string }>>([]);
  readonly blocks = signal<Array<{ id: number; name: string }>>([]);
  id: number | null = null;
  bankId = 0;
  blockId = 0;
  text = '';
  type = 'Seleccion multiple';
  topic = '';
  explanation = '';
  imageUrl = '';
  isActive = true;
  options: OptionDraft[] = [
    { text: '', isCorrect: true, imageUrl: '' },
    { text: '', isCorrect: false, imageUrl: '' }
  ];
  private lastType = 'Seleccion multiple';

  get isTrueFalse(): boolean {
    return this.type === 'Verdadero/Falso';
  }

  ngOnInit(): void {
    this.api.banks(false).subscribe({
      next: (banks) => {
        this.banks.set(banks);
        if (!this.bankId && banks[0]) {
          this.bankId = banks[0].id;
        }
      }
    });
    this.api.blocks().subscribe({
      next: (blocks) => {
        this.blocks.set(blocks);
        if (!this.blockId && blocks[0]) {
          this.blockId = blocks[0].id;
        }
      }
    });
    const raw = this.route.snapshot.paramMap.get('id');
    if (raw && raw !== 'new') {
      this.id = Number(raw);
      this.api.question(this.id).subscribe({
        next: (q) => {
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
        },
        error: (err) => this.error.set(mapApiError(err))
      });
    }
  }

  letter(index: number): string {
    return String.fromCharCode(65 + index);
  }

  onTypeChange(): void {
    if (this.type === this.lastType) {
      return;
    }
    this.lastType = this.type;
    if (!this.isTrueFalse) {
      return;
    }
    this.options = [
      { text: 'Verdadero', isCorrect: true, imageUrl: '' },
      { text: 'Falso', isCorrect: false, imageUrl: '' }
    ];
  }

  markCorrect(index: number): void {
    this.options = this.options.map((o, i) => ({
      ...o,
      isCorrect: i === index
    }));
  }

  addOption(): void {
    if (this.isTrueFalse) {
      return;
    }
    this.options = [...this.options, { text: '', isCorrect: false, imageUrl: '' }];
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
    }
  }

  uploadQuestionImage(file: File): void {
    this.upload(file, (url) => {
      this.imageUrl = url;
    }, 'question');
  }

  uploadOptionImage(index: number, file: File): void {
    this.upload(file, (url) => {
      this.options = this.options.map((o, i) =>
        i === index ? { ...o, imageUrl: url } : o);
    }, `opt-${index}`);
  }

  clearQuestionImage(): void {
    this.imageUrl = '';
  }

  clearOptionImage(index: number): void {
    this.options = this.options.map((o, i) =>
      i === index ? { ...o, imageUrl: '' } : o);
  }

  cancel(): void {
    void this.router.navigateByUrl(this.backUrl());
  }

  save(): void {
    this.error.set(null);
    if (!this.bankId || !this.blockId) {
      this.error.set('Elige banco y bloque.');
      return;
    }
    if (!this.text.trim()) {
      this.error.set('Escribe el enunciado de la pregunta.');
      return;
    }
    if (this.options.length < 2) {
      this.error.set('Agrega al menos dos respuestas.');
      return;
    }
    if (this.options.filter((o) => o.isCorrect).length !== 1) {
      this.error.set('Marca exactamente una respuesta correcta.');
      return;
    }
    if (this.options.some((o) => !o.text.trim() && !o.imageUrl)) {
      this.error.set('Cada respuesta necesita texto o imagen.');
      return;
    }
    const body = {
      bankId: this.bankId,
      blockId: this.blockId,
      text: this.text,
      type: this.type,
      topic: this.topic || null,
      imageUrl: this.imageUrl || null,
      explanation: this.explanation || null,
      isActive: this.isActive,
      options: this.options.map((o) => ({
        text: o.text,
        isCorrect: o.isCorrect,
        imageUrl: o.imageUrl || null
      }))
    };
    this.saving.set(true);
    this.api.saveQuestion(body, this.id ?? undefined).subscribe({
      next: () => {
        this.saving.set(false);
        this.ok.set('Pregunta guardada.');
        void this.router.navigateByUrl(this.backUrl());
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private upload(
    file: File,
    apply: (url: string) => void,
    key: string
  ): void {
    this.error.set(null);
    this.uploading.set(key);
    this.api.upload(file).subscribe({
      next: (res) => {
        apply(res.url);
        this.uploading.set(null);
      },
      error: (err: unknown) => {
        this.uploading.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  private backUrl(): string {
    return location.pathname.includes('/admin/')
      ? '/admin/questions'
      : '/teacher/questions';
  }
}
