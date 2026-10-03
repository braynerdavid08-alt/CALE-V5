import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { StudentCourseDetail, StudentCoursesApi } from '../api/courses.api';

@Component({
  selector: 'app-student-course-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiLoadingComponent, UiPageHeaderComponent],
  styleUrl: './courses.css',
  template: `
    <section class="courses-page narrow">
      <a class="back-link" routerLink="/student/cursos">← Todos los cursos</a>

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (loading()) {
        <ui-loading />
      }
      @if (!loading() && course(); as c) {
        <ui-page-header [eyebrow]="c.category" [title]="c.title" [subtitle]="c.description || ''">
          @if (nextId(); as next) {
            <ui-button [routerLink]="['/student/cursos/leccion', next]">
              {{ c.completedLessons ? 'Continuar' : 'Empezar curso' }}
            </ui-button>
          }
        </ui-page-header>

        <div class="panel">
          <div class="progress">
            <span class="bar"><span [style.width.%]="c.percent"></span></span>
            <small>
              {{ c.completedLessons }} de {{ c.totalLessons }} lecciones completadas · {{ c.percent }}%
              @if (c.percent >= 100) { · ¡Curso terminado! }
            </small>
          </div>
        </div>

        <ol class="lesson-list">
          @for (l of c.lessons; track l.id) {
            <li>
              <a class="lesson-row" [class.done]="l.completed" [routerLink]="['/student/cursos/leccion', l.id]">
                <span class="lesson-num">{{ l.completed ? '✓' : l.position }}</span>
                <span class="lesson-info">
                  <strong>{{ l.title }}</strong>
                  @if (l.summary) { <small>{{ l.summary }}</small> }
                </span>
                <span class="lesson-meta">
                  @if (l.completed && l.score !== null && l.score !== undefined) { {{ l.score }}% · }
                  {{ l.estimatedMinutes }} min
                </span>
              </a>
            </li>
          }
        </ol>
      }
    </section>
  `
})
export class StudentCoursePage implements OnInit {
  private readonly api = inject(StudentCoursesApi);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly course = signal<StudentCourseDetail | null>(null);
  readonly nextId = computed(() => {
    const c = this.course();
    return c?.lessons.find((l) => !l.completed)?.id ?? null;
  });

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.api.get(id).subscribe({
      next: (c) => {
        this.course.set(c);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }
}
