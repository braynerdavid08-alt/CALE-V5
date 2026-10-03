import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { StudentCourseItem, StudentCoursesApi } from '../api/courses.api';

@Component({
  selector: 'app-student-courses-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiLoadingComponent, UiPageHeaderComponent],
  styleUrl: './courses.css',
  template: `
    <section class="courses-page">
      <ui-page-header
        eyebrow="Aprende a tu ritmo"
        title="Cursos virtuales"
        subtitle="Lecciones cortas con señales, tarjetas y preguntas para reforzar lo que ves en clase. Ganas experiencia por cada lección." />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (loading()) {
        <ui-loading />
      } @else if (!courses().length) {
        <div class="panel empty">
          <h2>Todavía no hay cursos</h2>
          <p class="muted">Cuando tu escuela o Luz Verde publiquen un curso, aparecerá aquí.</p>
        </div>
      } @else {
        <div class="course-grid">
          @for (c of courses(); track c.id) {
            <article class="course-card">
              <a class="cover" [routerLink]="['/student/cursos', c.id]" [attr.aria-label]="c.title">
                @if (c.coverUrl) {
                  <img [src]="url(c.coverUrl)" alt="" loading="lazy" />
                } @else {
                  <span class="cover-fallback" aria-hidden="true">📘</span>
                }
              </a>
              <div class="course-body">
                <span class="tag">{{ c.category }}</span>
                <h2><a [routerLink]="['/student/cursos', c.id]">{{ c.title }}</a></h2>
                @if (c.description) { <p class="muted desc">{{ c.description }}</p> }
                <div class="progress">
                  <span class="bar"><span [style.width.%]="c.percent"></span></span>
                  <small>{{ c.completedLessons }} de {{ c.totalLessons }} lecciones</small>
                </div>
                <div class="card-actions">
                  @if (c.percent >= 100) {
                    <span class="chip good">✓ Completado</span>
                    <ui-button variant="secondary" [routerLink]="['/student/cursos', c.id]">Repasar</ui-button>
                  } @else if (c.nextLessonId) {
                    <ui-button [routerLink]="['/student/cursos/leccion', c.nextLessonId]">
                      {{ c.completedLessons ? 'Continuar' : 'Empezar' }}
                    </ui-button>
                  }
                </div>
              </div>
            </article>
          }
        </div>
      }
    </section>
  `
})
export class StudentCoursesPage implements OnInit {
  private readonly api = inject(StudentCoursesApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly courses = signal<StudentCourseItem[]>([]);

  ngOnInit(): void {
    this.api.list().subscribe({
      next: (list) => {
        this.courses.set(list);
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
}
