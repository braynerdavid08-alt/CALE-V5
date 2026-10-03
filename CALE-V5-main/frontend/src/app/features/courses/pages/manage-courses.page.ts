import { NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { CourseManageItem, CoursesManageApi } from '../api/courses.api';

@Component({
  selector: 'app-manage-courses-page',
  standalone: true,
  imports: [FormsModule, NgTemplateOutlet, RouterLink, UiButtonComponent, UiLoadingComponent, UiPageHeaderComponent],
  styleUrl: './courses.css',
  styles: [`
    .new-form { display: flex; flex-wrap: wrap; gap: 0.6rem; align-items: end; }
    .new-form .field { flex: 1 1 18rem; }
    h2.section { margin: 0.5rem 0 0; font-size: var(--text-lg); }
  `],
  template: `
    <section class="courses-page">
      <ui-page-header
        eyebrow="Cursos virtuales"
        title="Crea cursos para tus estudiantes"
        [subtitle]="isAdmin
          ? 'Los cursos que publiques aquí los ven todos los estudiantes de Luz Verde.'
          : 'Arma lecciones con textos, señales, imágenes, videos y preguntas. Solo los ven los estudiantes de tu escuela. No suman horas: son refuerzo.'" />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      <form class="panel new-form" (ngSubmit)="create()">
        <label class="field">
          Nombre del curso nuevo
          <input name="title" [(ngModel)]="newTitle" maxlength="160" placeholder="Ej.: Normas para motociclistas" />
        </label>
        <ui-button type="submit" [loading]="creating()" [disabled]="!newTitle.trim()">Crear curso</ui-button>
      </form>

      @if (loading()) {
        <ui-loading />
      } @else {
        @if (mine().length) {
          <h2 class="section">{{ isAdmin ? 'Cursos de Luz Verde' : 'Cursos de mi escuela' }}</h2>
          <div class="course-grid">
            @for (c of mine(); track c.id) {
              <ng-container *ngTemplateOutlet="card; context: { $implicit: c }" />
            }
          </div>
        } @else {
          <div class="panel empty">
            <h2>Aún no has creado cursos</h2>
            <p class="muted">Escribe un nombre arriba para empezar{{ isAdmin ? '' : ', o haz una copia de un curso de Luz Verde y ajústalo a tu escuela' }}.</p>
          </div>
        }

        @if (!isAdmin && platform().length) {
          <h2 class="section">Cursos de Luz Verde</h2>
          <p class="muted" style="margin: 0">Tus estudiantes ya los ven. Si quieres cambiar algo, haz una copia para tu escuela.</p>
          <div class="course-grid">
            @for (c of platform(); track c.id) {
              <ng-container *ngTemplateOutlet="card; context: { $implicit: c }" />
            }
          </div>
        }
      }

      <ng-template #card let-c>
        <article class="course-card">
          <a class="cover" [routerLink]="[base, c.id]" [attr.aria-label]="c.title">
            @if (c.coverUrl) {
              <img [src]="url(c.coverUrl)" alt="" loading="lazy" />
            } @else {
              <span class="cover-fallback" aria-hidden="true">📘</span>
            }
          </a>
          <div class="course-body">
            <div class="card-actions" style="margin: 0">
              <span class="tag">{{ c.category }}</span>
              <span class="tag" [class.gray]="!c.isPublished">{{ c.isPublished ? 'Publicado' : 'Borrador' }}</span>
            </div>
            <h2><a [routerLink]="[base, c.id]">{{ c.title }}</a></h2>
            <small class="muted">{{ c.lessonCount }} {{ c.lessonCount === 1 ? 'lección' : 'lecciones' }}</small>
            <div class="card-actions">
              <ui-button variant="secondary" [routerLink]="[base, c.id]">{{ c.canEdit ? 'Editar' : 'Ver' }}</ui-button>
              @if (!c.canEdit) {
                <ui-button variant="ghost" [loading]="duplicating() === c.id" (click)="duplicate(c)">Hacer una copia</ui-button>
              }
            </div>
          </div>
        </article>
      </ng-template>
    </section>
  `
})
export class ManageCoursesPage implements OnInit {
  private readonly api = inject(CoursesManageApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly base: string = this.route.snapshot.data['base'] ?? '/teacher/cursos';
  readonly isAdmin = this.base.startsWith('/admin');

  readonly loading = signal(true);
  readonly creating = signal(false);
  readonly duplicating = signal<number | null>(null);
  readonly error = signal<string | null>(null);
  readonly courses = signal<CourseManageItem[]>([]);
  readonly mine = computed(() => this.courses().filter((c) => c.canEdit));
  readonly platform = computed(() => this.courses().filter((c) => !c.canEdit));
  newTitle = '';

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

  create(): void {
    const title = this.newTitle.trim();
    if (!title || this.creating()) return;
    this.creating.set(true);
    this.error.set(null);
    this.api.create({ title, isPublished: false }).subscribe({
      next: (c) => this.router.navigate([this.base, c.id]),
      error: (err) => {
        this.creating.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  duplicate(c: CourseManageItem): void {
    this.duplicating.set(c.id);
    this.error.set(null);
    this.api.duplicate(c.id).subscribe({
      next: (copy) => this.router.navigate([this.base, copy.id]),
      error: (err) => {
        this.duplicating.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }
}
