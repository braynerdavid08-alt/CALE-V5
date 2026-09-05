import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { GroupDto } from '../../student/api/student.api';
import { QuestionReviewDto, TeacherApi } from '../../teacher/api/teacher.api';

@Component({
  selector: 'app-teacher-exam-review-page',
  standalone: true,
  imports: [
    FormsModule,
    UiButtonComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiPageHeaderComponent,
    UiSuccessComponent
  ],
  templateUrl: './teacher-exam-review.page.html',
  styleUrl: './teacher-exam-review.page.css'
})
export class TeacherExamReviewPage implements OnInit {
  private readonly api = inject(TeacherApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly items = signal<QuestionReviewDto[]>([]);
  readonly groups = signal<GroupDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly loading = signal(true);
  readonly savingId = signal<number | null>(null);
  readonly busy = signal(false);
  bankId = 0;
  examId: number | null = null;
  bankName = '';
  assignGroupId: number | null = null;

  readonly allReviewed = computed(() => !this.loading() && this.items().length === 0 && this.bankId > 0);

  ngOnInit(): void {
    this.api.groups().subscribe({ next: (items) => this.groups.set(items) });
    this.route.queryParamMap.subscribe((params) => {
      this.bankId = Number(params.get('bankId') || 0);
      const exam = Number(params.get('examId') || 0);
      this.examId = exam > 0 ? exam : null;
      this.bankName = params.get('name')?.trim() || '';
      if (this.bankId > 0) {
        this.reload();
      } else {
        this.loading.set(false);
        this.error.set('Falta el banco a revisar. Vuelve a Biblioteca e importa de nuevo.');
      }
    });
  }

  letter(index: number): string {
    return String.fromCharCode(65 + index);
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.questionsForReview(this.bankId).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
        if (items.length === 0) {
          this.ok.set('No quedan preguntas pendientes de clave. Puedes asignar o publicar.');
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  markCorrect(question: QuestionReviewDto, optionIndex: number): void {
    if (this.savingId()) {
      return;
    }
    const options = question.options.map((o, i) => ({
      text: o.text,
      isCorrect: i === optionIndex,
      imageUrl: o.imageUrl ?? null
    }));
    if (options.filter((o) => o.isCorrect).length !== 1) {
      this.error.set('Marca exactamente una respuesta correcta.');
      return;
    }

    this.savingId.set(question.id);
    this.error.set(null);
    this.api
      .saveQuestion(
        {
          bankId: question.bankId,
          blockId: question.blockId,
          text: question.text,
          type: question.type,
          topic: question.topic ?? null,
          imageUrl: null,
          explanation: null,
          isActive: question.isActive,
          options
        },
        question.id
      )
      .subscribe({
        next: () => {
          this.savingId.set(null);
          this.items.update((list) => list.filter((q) => q.id !== question.id));
          const left = this.items().length;
          this.ok.set(
            left === 0
              ? 'Todas las claves revisadas. Asigna a un grupo o publica.'
              : `Clave guardada. Quedan ${left}.`
          );
        },
        error: (err) => {
          this.savingId.set(null);
          this.error.set(mapApiError(err));
        }
      });
  }

  assignAndGo(): void {
    if (!this.examId) {
      this.error.set('No hay examen asociado para asignar.');
      return;
    }
    if (!this.assignGroupId) {
      this.error.set('Elige un grupo.');
      return;
    }
    this.busy.set(true);
    this.api.assignExam(this.examId, this.assignGroupId).subscribe({
      next: () => {
        this.busy.set(false);
        this.ok.set('Examen asignado al grupo.');
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  publishExam(): void {
    if (!this.examId) {
      this.error.set('No hay examen para publicar.');
      return;
    }
    this.busy.set(true);
    this.api.publishExam(this.examId, true).subscribe({
      next: () => {
        this.busy.set(false);
        this.ok.set('Examen publicado.');
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  backToLibrary(): void {
    void this.router.navigateByUrl('/teacher/library');
  }
}
