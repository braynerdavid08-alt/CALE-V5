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
        subtitle="Están organizados por núcleo. Cada uno tiene lecciones cortas, con señales, tarjetas y preguntas. Ganas experiencia por cada lección." />

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
        @for (group of groups(); track group.title) {
          <section class="nucleus">
            <h2>{{ group.title }}</h2>
            <p class="muted lede">{{ group.summary }}</p>
            <div class="course-grid">
          @for (c of group.courses; track c.id) {
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
          </section>
        }
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

  groups(): Array<{ title: string; summary: string; courses: StudentCourseItem[] }> {
    const used = new Set<number>();
    const grouped = NUCLEI.map((nucleus) => {
      const courses = this.courses().filter((course) =>
        nucleus.match.some((prefix) => course.title.startsWith(prefix)));
      courses.forEach((course) => used.add(course.id));
      return { title: nucleus.title, summary: nucleus.summary, courses };
    }).filter((group) => group.courses.length > 0);
    const other = this.courses().filter((course) => !used.has(course.id));
    if (other.length > 0) {
      grouped.push({
        title: 'Otros cursos',
        summary: 'Cursos publicados por tu escuela.',
        courses: other
      });
    }
    return grouped;
  }
}

const NUCLEI: Array<{ title: string; summary: string; match: string[] }> = [
  {
    title: 'Movilidad segura y sostenible',
    summary: 'Sistema Seguro, Visión Cero, víctimas, usuarios vulnerables, emergencias y eco-conducción.',
    match: ['Movilidad segura', 'Primeros auxilios']
  },
  {
    title: 'Normas de tránsito',
    summary: 'Código, documentos, velocidad, prelación, maniobras, carga e infracciones.',
    match: ['Normas de tránsito']
  },
  {
    title: 'Señalización e infraestructura vial',
    summary: 'Señales, demarcación, semáforos, la vía, ciclistas y espacio público.',
    match: ['Señales de tránsito', 'Señalización vial', 'La vía y el espacio']
  },
  {
    title: 'El vehículo',
    summary: 'Sistemas, revisión, seguridad activa y pasiva, y qué hacer ante una avería.',
    match: ['El vehículo']
  },
  {
    title: 'Motocicleta (A2)',
    summary: 'Protección, revisión, frenado, curvas, clima, acompañante y carga.',
    match: ['Conducción segura en motocicleta']
  },
  {
    title: 'Automóvil (B1)',
    summary: 'Puesto de conducción, cambios, frenado, estacionamiento y giros.',
    match: ['Dominio seguro del automóvil']
  },
  {
    title: 'Servicio público (C1)',
    summary: 'Documentos del servicio, atención al usuario, fatiga y rutas de Barranquilla.',
    match: ['Conducción profesional de servicio público']
  }
];
