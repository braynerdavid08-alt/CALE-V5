import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiIconComponent } from '../../../shared/ui/ui-icon.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { ExamDto } from '../../student/api/exam.api';
import { GroupDto } from '../../student/api/student.api';
import { BankAdminDto, TeacherApi } from '../api/teacher.api';

type LibraryTab = 'recent' | 'drafts' | 'published';
type ViewMode = 'grid' | 'list';

@Component({
  selector: 'app-teacher-library-page',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    UiBadgeComponent,
    UiButtonComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiIconComponent,
    UiSuccessComponent
  ],
  templateUrl: './teacher-library.page.html',
  styleUrl: './teacher-library.page.css'
})
export class TeacherLibraryPage implements OnInit {
  private readonly api = inject(TeacherApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly session = inject(SessionStore);

  readonly exams = signal<ExamDto[]>([]);
  readonly banks = signal<BankAdminDto[]>([]);
  readonly groups = signal<GroupDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly tab = signal<LibraryTab>('recent');
  readonly view = signal<ViewMode>('grid');
  readonly query = signal('');
  readonly showCreate = signal(false);
  readonly editingId = signal<number | null>(null);
  readonly menuFor = signal<number | null>(null);
  readonly importing = signal(false);
  readonly saving = signal(false);

  name = '';
  description = '';
  bankId: number | null = null;
  questionCount = 20;
  timeMinutes = 30;
  allowedAttempts = 1;
  randomize = true;
  startsAt = '';
  endsAt = '';
  importTitle = '';
  importFile: File | null = null;
  assignTo: Record<number, number | null> = {};

  readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    let items = [...this.exams()];
    const t = this.tab();
    if (t === 'drafts') {
      items = items.filter((e) => !e.published);
    } else if (t === 'published') {
      items = items.filter((e) => e.published);
    }
    if (q) {
      items = items.filter((e) => e.name.toLowerCase().includes(q));
    }
    return items;
  });

  readonly authorLabel = computed(
    () => this.session.user()?.email?.split('@')[0]
      || this.session.user()?.name
      || 'instructor'
  );

  ngOnInit(): void {
    this.reload();
    this.api.banks(true).subscribe({ next: (items) => this.banks.set(items) });
    this.api.groups().subscribe({ next: (items) => this.groups.set(items) });

    this.route.queryParamMap.subscribe((params) => {
      const q = params.get('q') ?? '';
      this.query.set(q);
      if (params.get('crear') === '1') {
        this.showCreate.set(true);
      }
    });
  }

  setTab(tab: LibraryTab): void {
    this.tab.set(tab);
  }

  setView(view: ViewMode): void {
    this.view.set(view);
  }

  onLocalSearch(value: string): void {
    this.query.set(value);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { q: value.trim() || null },
      queryParamsHandling: 'merge'
    });
  }

  reload(): void {
    this.api.exams().subscribe({
      next: (items) => this.exams.set(items),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  openCreate(): void {
    this.resetForm();
    this.editingId.set(null);
    this.showCreate.set(true);
    this.scrollToForm();
  }

  openEdit(exam: ExamDto): void {
    this.menuFor.set(null);
    this.editingId.set(exam.id);
    this.name = exam.name;
    this.description = exam.description ?? '';
    this.bankId = exam.bankId ?? null;
    this.questionCount = exam.questionCount;
    this.timeMinutes = exam.timeMinutes;
    this.allowedAttempts = exam.allowedAttempts;
    this.randomize = exam.randomize !== false;
    this.startsAt = this.toLocalInput(exam.startsAt);
    this.endsAt = this.toLocalInput(exam.endsAt);
    this.showCreate.set(true);
    this.scrollToForm();
  }

  focusImport(): void {
    document.getElementById('exam-import')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  onImportFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.importFile = input.files?.[0] ?? null;
  }

  importExam(): void {
    if (!this.importFile) {
      this.error.set('Elige un archivo Word (.docx).');
      return;
    }
    this.importing.set(true);
    this.error.set(null);
    this.api.importExamFromWord(this.importFile, this.importTitle || undefined).subscribe({
      next: (result) => {
        this.importing.set(false);
        this.importFile = null;
        this.importTitle = '';
        this.reload();
        this.api.banks(true).subscribe({ next: (items) => this.banks.set(items) });
        const skipped =
          result.skippedCount > 0 ? ` Se omitieron ${result.skippedCount}.` : '';
        if (result.needsCorrectReview > 0) {
          this.ok.set(
            `Importado “${result.name}”: ${result.importedQuestions} preguntas.${skipped} Abriendo revisión de ${result.needsCorrectReview} sin clave…`
          );
          void this.router.navigate(['/teacher/exam-review'], {
            queryParams: {
              bankId: result.bankId,
              examId: result.examId,
              name: result.name
            }
          });
          return;
        }
        this.ok.set(
          `Importado “${result.name}”: ${result.importedQuestions} preguntas.${skipped}`
        );
      },
      error: (err) => {
        this.importing.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  downloadExamTemplate(): void {
    this.api.downloadExamImportTemplate().subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'cale-plantilla-examen.docx';
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  closeCreate(): void {
    this.showCreate.set(false);
    this.editingId.set(null);
    this.resetForm();
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { crear: null },
      queryParamsHandling: 'merge'
    });
  }

  saveExam(): void {
    if (!this.name.trim()) {
      this.error.set('Escribe el nombre del examen.');
      return;
    }
    const body = {
      name: this.name.trim(),
      description: this.description.trim() || null,
      bankId: this.bankId,
      questionCount: this.questionCount,
      timeMinutes: this.timeMinutes,
      allowedAttempts: this.allowedAttempts,
      randomize: this.randomize,
      startsAt: this.startsAt ? new Date(this.startsAt).toISOString() : null,
      endsAt: this.endsAt ? new Date(this.endsAt).toISOString() : null
    };
    const id = this.editingId();
    this.saving.set(true);
    this.error.set(null);
    const req = id ? this.api.updateExam(id, body) : this.api.createExam(body);
    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.closeCreate();
        this.reload();
        this.ok.set(id ? 'Examen actualizado.' : 'Examen creado.');
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  toggleMenu(id: number, event: Event): void {
    event.stopPropagation();
    this.menuFor.update((current) => (current === id ? null : id));
  }

  togglePublish(exam: ExamDto): void {
    this.menuFor.set(null);
    this.api.publishExam(exam.id, !exam.published).subscribe({
      next: () => {
        this.ok.set(exam.published ? 'Pasó a borrador.' : 'Examen publicado.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  exportExam(exam: ExamDto): void {
    this.menuFor.set(null);
    this.api.exportExamToWord(exam.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${exam.name || 'examen'}.docx`;
        a.click();
        URL.revokeObjectURL(url);
        this.ok.set(`Exportado “${exam.name}”.`);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  reviewKeys(exam: ExamDto): void {
    this.menuFor.set(null);
    if (!exam.bankId) {
      this.error.set('Este examen no tiene banco para revisar.');
      return;
    }
    void this.router.navigate(['/teacher/exam-review'], {
      queryParams: {
        bankId: exam.bankId,
        examId: exam.id,
        name: exam.name
      }
    });
  }

  assign(examId: number): void {
    const groupId = this.assignTo[examId];
    if (!groupId) {
      this.error.set('Elige un grupo.');
      return;
    }
    this.menuFor.set(null);
    this.api.assignExam(examId, groupId).subscribe({
      next: () => {
        this.ok.set('Examen asignado al grupo.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  thumbTone(exam: ExamDto): string {
    const n = exam.id % 4;
    return `tone-${n}`;
  }

  private resetForm(): void {
    this.name = '';
    this.description = '';
    this.bankId = null;
    this.questionCount = 20;
    this.timeMinutes = 30;
    this.allowedAttempts = 1;
    this.randomize = true;
    this.startsAt = '';
    this.endsAt = '';
  }

  private scrollToForm(): void {
    queueMicrotask(() => {
      document.querySelector('.create-panel')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  }

  private toLocalInput(iso: string | null | undefined): string {
    if (!iso) {
      return '';
    }
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) {
      return '';
    }
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
}
