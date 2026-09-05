import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { QuestionReviewDto, TeacherApi } from '../../teacher/api/teacher.api';

@Component({
  selector: 'app-teacher-exam-review-page',
  standalone: true,
  imports: [
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
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly loading = signal(true);
  readonly savingId = signal<number | null>(null);
  bankId = 0;
  examId: number | null = null;
  bankName = '';

  ngOnInit(): void {
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
          this.ok.set('No quedan preguntas pendientes de clave en este banco.');
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
              ? 'Todas las claves revisadas. Ya puedes publicar el examen.'
              : `Clave guardada. Quedan ${left}.`
          );
        },
        error: (err) => {
          this.savingId.set(null);
          this.error.set(mapApiError(err));
        }
      });
  }

  backToLibrary(): void {
    void this.router.navigateByUrl('/teacher/library');
  }
}
