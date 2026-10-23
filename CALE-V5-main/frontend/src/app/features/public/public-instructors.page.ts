import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { mapApiError } from '../../core/http/map-api-error';
import { BRAND, brandPageTitle } from '../../core/brand';
import { env } from '../../core/config/env';
import { SessionStore } from '../../core/auth/session.store';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { UiSuccessComponent } from '../../shared/ui/ui-success.component';
import { PublicHomeApi } from './public-home.api';
import { PublicInstructorCardDto } from './public.models';

type Mode = 'browse' | 'contratar' | 'calificar';

interface ReviewDto {
  id: number;
  stars: number;
  comment: string;
  authorName: string;
  updatedAt: string;
}

@Component({
  selector: 'app-public-instructors-page',
  standalone: true,
  imports: [
    DecimalPipe,
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiErrorComponent,
    UiIconComponent,
    UiLoadingComponent,
    UiSuccessComponent
  ],
  template: `
    <div class="page">
      <header class="head">
        <p class="eyebrow">Instructores</p>
        <h1>Instructores de Luz Verde</h1>
        <p class="lead">
          Directorio público. Puedes pedir una clase particular o dejar una calificación.
          Luz Verde solo conecta: el acuerdo y el pago se hacen directamente con el instructor.
        </p>
        <div class="filters">
          <button type="button" class="chip" [class.on]="mode() === 'browse'" (click)="setMode('browse')">Todos</button>
          <button type="button" class="chip" [class.on]="mode() === 'contratar'" (click)="setMode('contratar')">Contratar clase</button>
          <button type="button" class="chip" [class.on]="mode() === 'calificar'" (click)="setMode('calificar')">Calificar</button>
        </div>
        @if (mode() === 'contratar') {
          <p class="hint">Elige un instructor que acepte clases particulares.</p>
        }
        @if (mode() === 'calificar') {
          <p class="hint">Elige a quién quieres calificar. Necesitas haber iniciado sesión.</p>
        }
      </header>

      <ui-error [message]="error()" />
      <ui-success [message]="ok()" />

      @if (loading()) {
        <ui-loading />
      } @else if (!visible().length) {
        <p class="empty">
          @if (mode() === 'contratar') {
            Ahora mismo ningún instructor publicado acepta clases particulares.
          } @else {
            Aún no hay instructores publicados.
          }
        </p>
        <ui-button routerLink="/register-teacher" type="button">Registrarme como instructor</ui-button>
      } @else {
        <div class="grid">
          @for (ins of visible(); track ins.id) {
            <article class="card">
              <div class="top-row">
                <div class="avatar" aria-hidden="true"><ui-icon name="instructor" /></div>
                @if (ins.averageStars) {
                  <p class="stars" title="Promedio de calificaciones">
                    ★ {{ ins.averageStars | number:'1.1-1' }}
                    <span>({{ ins.reviewCount || 0 }})</span>
                  </p>
                }
              </div>
              <h2>{{ ins.displayName }}</h2>
              <p class="muted">{{ ins.schoolName || ('Instructor ' + brand.name) }}</p>
              @if (ins.acceptsPrivateClasses) {
                <p class="badge">Acepta clases particulares</p>
              }
              <div class="actions">
                @if (ins.acceptsPrivateClasses) {
                  <ui-button type="button" (click)="openHire(ins)">Contratar</ui-button>
                }
                <ui-button type="button" variant="secondary" (click)="openRate(ins)">Calificar</ui-button>
                <ui-button type="button" variant="ghost" (click)="toggleReviews(ins)">Opiniones</ui-button>
              </div>
              @if (expanded() === ins.id) {
                <div class="reviews">
                  @if (reviewsLoading() === ins.id) {
                    <p class="muted">Cargando…</p>
                  } @else if (!reviewsFor(ins.id).length) {
                    <p class="muted">Todavía no hay opiniones públicas.</p>
                  } @else {
                    @for (r of reviewsFor(ins.id); track r.id) {
                      <blockquote>
                        <strong>{{ '★'.repeat(r.stars) }}</strong>
                        <p>{{ r.comment }}</p>
                        <cite>{{ r.authorName }}</cite>
                      </blockquote>
                    }
                  }
                </div>
              }
            </article>
          }
        </div>
      }
    </div>

    @if (hireTarget(); as h) {
      <div class="modal" role="dialog" aria-modal="true" aria-labelledby="hire-title">
        <div class="sheet">
          <h2 id="hire-title">Pedir clase a {{ h.displayName }}</h2>
          <p class="muted">Cuéntale qué necesitas. Si acepta, verás cómo contactarlo. No hay pago dentro de Luz Verde.</p>
          @if (!isStudent()) {
            <p class="warn">Para pedir una clase debes iniciar sesión como estudiante.</p>
            <div class="actions">
              <ui-button routerLink="/login" type="button">Iniciar sesión</ui-button>
              <ui-button type="button" variant="ghost" (click)="hireTarget.set(null)">Cerrar</ui-button>
            </div>
          } @else {
            <label class="field">Categoría
              <select [(ngModel)]="hireCategory">
                <option value="A2">A2 · Motocicleta</option>
                <option value="B1">B1 · Automóvil</option>
                <option value="C1">C1 · Servicio público</option>
                <option value="Otra">Otra</option>
              </select>
            </label>
            <label class="field">Días y horarios que te sirven
              <input [(ngModel)]="hireSchedule" maxlength="200" placeholder="Ej. sábados 8–12 a. m." />
            </label>
            <label class="field">Mensaje
              <textarea [(ngModel)]="hireMessage" rows="4" maxlength="1000"
                placeholder="Qué quieres practicar y en qué ciudad…"></textarea>
            </label>
            <div class="actions">
              <ui-button type="button" [disabled]="busy()" (click)="submitHire()">Enviar solicitud</ui-button>
              <ui-button type="button" variant="ghost" (click)="hireTarget.set(null)">Cancelar</ui-button>
            </div>
          }
        </div>
      </div>
    }

    @if (rateTarget(); as r) {
      <div class="modal" role="dialog" aria-modal="true" aria-labelledby="rate-title">
        <div class="sheet">
          <h2 id="rate-title">Calificar a {{ r.displayName }}</h2>
          @if (!loggedIn()) {
            <p class="warn">Inicia sesión para dejar tu calificación.</p>
            <div class="actions">
              <ui-button routerLink="/login" type="button">Iniciar sesión</ui-button>
              <ui-button type="button" variant="ghost" (click)="rateTarget.set(null)">Cerrar</ui-button>
            </div>
          } @else {
            <label class="field">Estrellas
              <select [(ngModel)]="rateStars">
                <option [ngValue]="5">5 · Excelente</option>
                <option [ngValue]="4">4 · Muy bueno</option>
                <option [ngValue]="3">3 · Bueno</option>
                <option [ngValue]="2">2 · Regular</option>
                <option [ngValue]="1">1 · Malo</option>
              </select>
            </label>
            <label class="field">Comentario (opcional)
              <textarea [(ngModel)]="rateComment" rows="3" maxlength="500"
                placeholder="Qué te ayudó o qué mejorarías…"></textarea>
            </label>
            <div class="actions">
              <ui-button type="button" [disabled]="busy()" (click)="submitRate()">Guardar</ui-button>
              <ui-button type="button" variant="ghost" (click)="rateTarget.set(null)">Cancelar</ui-button>
            </div>
          }
        </div>
      </div>
    }
  `,
  styles: [`
    .page {
      width: min(1120px, calc(100% - 2rem));
      margin: 0 auto;
      padding: 2.5rem 0 3.5rem;
    }
    .eyebrow {
      margin: 0 0 0.35rem;
      color: var(--color-primary);
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }
    h1 { margin: 0 0 0.5rem; }
    .lead {
      margin: 0 0 1rem;
      color: var(--color-text-secondary);
      max-width: 44rem;
      line-height: var(--leading-body);
    }
    .filters { display: flex; flex-wrap: wrap; gap: 0.45rem; margin-bottom: 0.75rem; }
    .chip {
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      border-radius: 999px;
      padding: 0.4rem 0.85rem;
      font-weight: 700;
      font-size: var(--text-sm);
      cursor: pointer;
    }
    .chip.on {
      background: var(--color-primary);
      border-color: var(--color-primary);
      color: #fff;
    }
    .hint { margin: 0 0 1rem; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1rem;
    }
    .card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 1.2rem;
      box-shadow: var(--shadow-sm);
      display: grid;
      gap: 0.4rem;
      align-content: start;
    }
    .top-row { display: flex; justify-content: space-between; align-items: flex-start; }
    .avatar {
      width: 2.5rem; height: 2.5rem; border-radius: 50%;
      background: var(--color-primary-soft); color: var(--color-primary);
      display: grid; place-items: center;
    }
    .stars { margin: 0; font-weight: 800; color: #c48a00; font-size: var(--text-sm); }
    .stars span { color: var(--color-text-secondary); font-weight: 600; }
    h2 { margin: 0; font-size: var(--text-md); }
    .muted { margin: 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .badge {
      margin: 0.2rem 0 0;
      color: var(--color-primary);
      font-size: var(--text-xs);
      font-weight: 800;
    }
    .actions { display: flex; flex-wrap: wrap; gap: 0.45rem; margin-top: 0.45rem; }
    .actions a { text-decoration: none; }
    .reviews { display: grid; gap: 0.55rem; margin-top: 0.65rem; }
    blockquote {
      margin: 0; padding: 0.65rem 0.75rem;
      background: var(--color-background);
      border-radius: 0.65rem;
      border: 1px solid var(--color-border);
    }
    blockquote p { margin: 0.25rem 0; font-size: var(--text-sm); }
    blockquote cite { font-style: normal; color: var(--color-text-secondary); font-size: var(--text-xs); }
    .empty { color: var(--color-text-secondary); margin: 0 0 1rem; }
    .modal {
      position: fixed; inset: 0; z-index: 80;
      background: rgba(0,0,0,0.45);
      display: grid; place-items: center;
      padding: 1rem;
    }
    .sheet {
      width: min(440px, 100%);
      background: var(--color-surface);
      border-radius: var(--radius-lg);
      border: 1px solid var(--color-border);
      padding: 1.25rem;
      display: grid;
      gap: 0.75rem;
      box-shadow: var(--shadow-lg, 0 16px 40px rgba(0,0,0,0.25));
    }
    .sheet h2 { margin: 0; font-size: var(--text-lg); }
    .field { display: grid; gap: 0.3rem; font-weight: 700; font-size: var(--text-sm); }
    .field input, .field select, .field textarea {
      font: inherit; font-weight: 500;
      border: 1px solid var(--color-border);
      border-radius: 0.55rem;
      padding: 0.55rem 0.65rem;
      background: var(--color-background);
      color: var(--color-text);
    }
    .warn { margin: 0; color: #9a6700; font-size: var(--text-sm); }
    @media (max-width: 900px) { .grid { grid-template-columns: 1fr 1fr; } }
    @media (max-width: 600px) { .grid { grid-template-columns: 1fr; } }
  `]
})
export class PublicInstructorsPage implements OnInit {
  readonly brand = BRAND;
  private readonly api = inject(PublicHomeApi);
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly instructors = signal<PublicInstructorCardDto[]>([]);
  readonly mode = signal<Mode>('browse');
  readonly hireTarget = signal<PublicInstructorCardDto | null>(null);
  readonly rateTarget = signal<PublicInstructorCardDto | null>(null);
  readonly expanded = signal<number | null>(null);
  readonly reviews = signal<Record<number, ReviewDto[]>>({});
  readonly reviewsLoading = signal<number | null>(null);
  readonly busy = signal(false);
  readonly loggedIn = this.session.isAuthenticated;
  readonly isStudent = computed(() => this.session.user()?.role === 'Student');

  hireCategory = 'B1';
  hireSchedule = '';
  hireMessage = '';
  rateStars = 5;
  rateComment = '';

  readonly visible = computed(() => {
    const all = this.instructors();
    return this.mode() === 'contratar'
      ? all.filter((i) => i.acceptsPrivateClasses)
      : all;
  });

  ngOnInit(): void {
    this.title.setTitle(brandPageTitle('Instructores'));
    this.meta.updateTag({
      name: 'description',
      content: 'Directorio de instructores de Luz Verde: pide clases particulares o califica a tu instructor.'
    });

    const accion = this.route.snapshot.queryParamMap.get('accion');
    if (accion === 'contratar' || accion === 'calificar') {
      this.mode.set(accion);
    }

    this.api.listInstructors(48).subscribe({
      next: (list) => {
        this.instructors.set(list);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  setMode(mode: Mode): void {
    this.mode.set(mode);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: mode === 'browse' ? {} : { accion: mode },
      replaceUrl: true
    });
  }

  openHire(ins: PublicInstructorCardDto): void {
    this.error.set(null);
    this.ok.set(null);
    this.hireTarget.set(ins);
  }

  openRate(ins: PublicInstructorCardDto): void {
    this.error.set(null);
    this.ok.set(null);
    this.rateTarget.set(ins);
    if (this.loggedIn()) {
      this.http.get<{ stars: number; comment: string | null } | null>(
        `${env.apiUrl}/api/instructors/${ins.id}/rating/mine`
      ).subscribe({
        next: (mine) => {
          if (mine) {
            this.rateStars = mine.stars;
            this.rateComment = mine.comment ?? '';
          }
        }
      });
    }
  }

  reviewsFor(instructorId: number): ReviewDto[] {
    return this.reviews()[instructorId] ?? [];
  }

  toggleReviews(ins: PublicInstructorCardDto): void {
    if (this.expanded() === ins.id) {
      this.expanded.set(null);
      return;
    }
    this.expanded.set(ins.id);
    if (this.reviews()[ins.id]) return;
    this.reviewsLoading.set(ins.id);
    this.http.get<ReviewDto[]>(`${env.apiUrl}/api/instructors/${ins.id}/reviews`).subscribe({
      next: (list) => {
        this.reviews.update((m) => ({ ...m, [ins.id]: list }));
        this.reviewsLoading.set(null);
      },
      error: () => this.reviewsLoading.set(null)
    });
  }

  submitHire(): void {
    const target = this.hireTarget();
    if (!target) return;
    this.busy.set(true);
    this.error.set(null);
    this.http.post(`${env.apiUrl}/api/private-classes`, {
      instructorId: target.id,
      category: this.hireCategory,
      preferredSchedule: this.hireSchedule,
      message: this.hireMessage
    }).subscribe({
      next: () => {
        this.busy.set(false);
        this.hireTarget.set(null);
        this.hireSchedule = '';
        this.hireMessage = '';
        this.ok.set(`Solicitud enviada a ${target.displayName}. Te avisaremos cuando responda.`);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  submitRate(): void {
    const target = this.rateTarget();
    if (!target) return;
    this.busy.set(true);
    this.error.set(null);
    this.http.put(`${env.apiUrl}/api/instructors/${target.id}/rating`, {
      stars: this.rateStars,
      comment: this.rateComment || null
    }).subscribe({
      next: () => {
        this.busy.set(false);
        this.rateTarget.set(null);
        this.ok.set(`Calificación guardada para ${target.displayName}.`);
        this.api.listInstructors(48).subscribe({ next: (list) => this.instructors.set(list) });
        this.reviews.update((m) => {
          const copy = { ...m };
          delete copy[target.id];
          return copy;
        });
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
