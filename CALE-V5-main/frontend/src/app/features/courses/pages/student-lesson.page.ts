import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { Badge } from '../../play/api/play.api';
import { PlayBadgesToastComponent } from '../../play/components/play-badges-toast.component';
import { PlayFxService } from '../../play/play-fx.service';
import { ActivityAnswer, LessonCompleteResult, StudentCoursesApi, StudentLesson } from '../api/courses.api';
import { ActivityAnswered, LessonBlocksComponent, QuizChecker } from '../components/lesson-blocks.component';

@Component({
  selector: 'app-student-lesson-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiLoadingComponent, LessonBlocksComponent, PlayBadgesToastComponent],
  styleUrl: './courses.css',
  template: `
    <section class="courses-page narrow">
      @if (lesson(); as l) {
        <a class="back-link" [routerLink]="['/student/cursos', l.courseId]">← {{ l.courseTitle }}</a>
      }

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (loading()) {
        <ui-loading />
      }
      @if (!loading() && lesson(); as l) {
        <header class="panel">
          <span class="tag">Lección {{ l.position }} de {{ l.totalLessons }} · {{ l.estimatedMinutes }} min</span>
          <h1 style="margin: 0.5rem 0 0.25rem; font-size: clamp(1.35rem, 3vw, 1.8rem)">{{ l.title }}</h1>
          @if (l.summary) { <p class="muted" style="margin: 0">{{ l.summary }}</p> }
          @if (l.completed) {
            <p class="ok-msg" style="margin-top: 0.75rem">Ya completaste esta lección (mejor nota {{ l.bestScore }}%). Puedes repasarla cuando quieras.</p>
          }
        </header>

        <div class="panel">
          <course-lesson-blocks [blocks]="l.content" [checker]="checker" (answered)="onAnswered($event)" />
        </div>

        @if (result(); as r) {
          <div class="panel result" role="status">
            <p class="big">{{ r.score }}%</p>
            <h2 style="margin: 0">{{ r.courseCompleted ? '¡Terminaste el curso!' : '¡Lección completada!' }}</h2>
            <p class="muted">
              Llevas {{ r.completedLessons }} de {{ r.totalLessons }} lecciones.
              @if (r.firstCompletion) { Ganaste experiencia para tu nivel. }
              @if (r.bestScore > r.score) { Tu mejor nota sigue siendo {{ r.bestScore }}%. }
            </p>
            <div class="card-actions">
              @if (r.nextLessonId) {
                <ui-button [routerLink]="['/student/cursos/leccion', r.nextLessonId]">Siguiente lección</ui-button>
              }
              <ui-button variant="secondary" [routerLink]="['/student/cursos', l.courseId]">Ver el curso</ui-button>
            </div>
          </div>
        } @else {
          <div class="panel" style="display: grid; gap: 0.6rem">
            @if (l.quizCount) {
              <p class="muted" style="margin: 0">
                Respondiste {{ answeredCount() }} de {{ l.quizCount }} actividades.
                @if (answeredCount() < l.quizCount) { Puedes terminar igual, pero las actividades sin responder cuentan como incorrectas. }
              </p>
            }
            <div class="player-nav">
              @if (l.previousLessonId) {
                <ui-button variant="ghost" [routerLink]="['/student/cursos/leccion', l.previousLessonId]">← Anterior</ui-button>
              } @else {
                <span></span>
              }
              <ui-button [loading]="saving()" (click)="complete()">Terminar lección</ui-button>
            </div>
          </div>
        }
      }
      <play-badges-toast [badges]="newBadges()" (closed)="newBadges.set([])" />
    </section>
  `
})
export class StudentLessonPage implements OnInit {
  private readonly api = inject(StudentCoursesApi);
  private readonly route = inject(ActivatedRoute);
  private readonly fx = inject(PlayFxService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly lesson = signal<StudentLesson | null>(null);
  readonly result = signal<LessonCompleteResult | null>(null);
  readonly newBadges = signal<Badge[]>([]);
  readonly answeredCount = signal(0);
  private answers: Record<number, ActivityAnswer> = {};

  readonly checker: QuizChecker = (blockIndex, answer) => this.api.check(this.lesson()!.id, blockIndex, answer);

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      this.load(Number(params.get('lessonId')));
    });
  }

  onAnswered(e: ActivityAnswered): void {
    this.answers[e.blockIndex] = e.value;
    this.answeredCount.set(Object.keys(this.answers).length);
    this.fx.play(e.correct ? 'correct' : 'wrong');
  }

  complete(): void {
    const l = this.lesson();
    if (!l || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    this.api.complete(l.id, this.answers).subscribe({
      next: (res) => {
        this.saving.set(false);
        this.result.set(res.result);
        this.fx.play('win');
        if (res.result.courseCompleted) this.fx.confetti(140);
        if (res.newBadges.length) {
          this.newBadges.set(res.newBadges);
          this.fx.play('badge');
        }
        window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private load(lessonId: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.result.set(null);
    this.answers = {};
    this.answeredCount.set(0);
    this.api.lesson(lessonId).subscribe({
      next: (l) => {
        this.lesson.set(l);
        this.loading.set(false);
        window.scrollTo({ top: 0 });
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }
}
