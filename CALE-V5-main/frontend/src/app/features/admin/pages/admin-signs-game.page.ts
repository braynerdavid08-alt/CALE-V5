import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { PlayApi, SignImageReport, SignsReport } from '../../play/api/play.api';

@Component({
  selector: 'app-admin-signs-game-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiErrorComponent, UiLoadingComponent, UiPageHeaderComponent],
  styles: [`
    :host { display: block; }
    .stats { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0.75rem; margin-bottom: 1.25rem; }
    .stat { padding: 1rem 1.1rem; border-radius: 1rem; border: 1px solid var(--color-border); background: var(--color-surface); }
    .stat strong { display: block; font-size: 1.8rem; line-height: 1.1; }
    .stat span { color: var(--color-text-secondary); font-size: var(--text-sm); }
    .stat.good strong { color: var(--color-success); }
    .stat.warn strong { color: #b45309; }
    h2 { margin: 1.5rem 0 0.6rem; font-size: var(--text-lg); }
    .hint { margin: 0 0 0.8rem; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .list { display: grid; gap: 0.6rem; margin: 0; padding: 0; list-style: none; }
    .row {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem 1rem;
      align-items: center;
      justify-content: space-between;
      padding: 0.85rem 1rem;
      border-radius: 0.9rem;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
    }
    .row-main { display: grid; gap: 0.2rem; min-width: 0; flex: 1 1 16rem; }
    .row-title { margin: 0; font-weight: 800; overflow-wrap: anywhere; }
    .meta { color: var(--color-text-secondary); font-size: var(--text-sm); }
    .bar { height: 0.45rem; border-radius: 999px; background: var(--color-surface-raised); overflow: hidden; }
    .bar > i { display: block; height: 100%; background: var(--color-success); }
    .tag { padding: 0.2rem 0.6rem; border-radius: 999px; font-size: var(--text-xs); font-weight: 800; white-space: nowrap; }
    .tag.admin { background: var(--color-primary-soft); color: var(--color-primary); }
    .tag.reason { background: color-mix(in srgb, #f59e0b 18%, transparent); color: #b45309; }
    .edit {
      padding: 0.45rem 0.9rem;
      border-radius: 999px;
      background: var(--color-primary-soft);
      color: var(--color-primary);
      font-weight: 800;
      text-decoration: none;
      white-space: nowrap;
    }
    .empty { text-align: center; padding: 1.25rem; color: var(--color-text-secondary); border: 1px dashed var(--color-border); border-radius: 1rem; }
    .actions { display: flex; justify-content: flex-end; margin-bottom: 0.75rem; }
    .thumb { width: 3.5rem; height: 3.5rem; object-fit: contain; border-radius: 0.5rem; background: #fff; flex: none; }
    @media (max-width: 640px) {
      .stats { grid-template-columns: 1fr 1fr; }
      .stats .stat:first-child { grid-column: 1 / -1; }
      .edit { flex: 1; text-align: center; }
    }
  `],
  template: `
    <ui-page-header
      eyebrow="Contenido"
      title="Señal relámpago"
      subtitle="Qué preguntas usa el juego. Solo entran preguntas de exámenes con “señal” en el nombre, creados por el administrador o guardadas en bancos oficiales, que tengan imagen y una única respuesta correcta." />
    @if (error()) { <ui-error [message]="error()" /> }

    <div class="actions">
      <ui-button type="button" variant="ghost" [loading]="loading()" (click)="load()">Actualizar</ui-button>
    </div>

    @if (loading() && !report()) {
      <ui-loading />
    }
    @if (report(); as r) {
      <div class="stats">
        <div class="stat good"><strong>{{ r.inGame }}</strong><span>preguntas en el juego</span></div>
        <div class="stat"><strong>{{ r.candidates }}</strong><span>preguntas permitidas en exámenes de señales</span></div>
        <div class="stat" [class.warn]="r.issues.length > 0"><strong>{{ r.issues.length }}</strong><span>por corregir</span></div>
      </div>

      <h2>Exámenes de señales</h2>
      @if (r.exams.length === 0) {
        <p class="empty">No hay exámenes activos con “señal” en el nombre. Crea uno o renombra un examen existente.</p>
      } @else {
        <ul class="list">
          @for (e of r.exams; track e.examId) {
            <li class="row">
              <div class="row-main">
                <p class="row-title">{{ e.name }}</p>
                <span class="meta">
                  {{ e.inGame }} de {{ e.questions }} en el juego
                  @if (e.notOfficial > 0) { · {{ e.notOfficial }} fuera de bancos oficiales (no cuentan) }
                </span>
                <div class="bar" aria-hidden="true"><i [style.width.%]="percent(e.inGame, e.questions)"></i></div>
              </div>
              @if (e.adminOwned) { <span class="tag admin">Del administrador</span> }
            </li>
          }
        </ul>
      }

      <h2>Preguntas por corregir</h2>
      <p class="hint">Están en un examen de señales permitido pero no salen en el juego. Edítalas para agregar la imagen o arreglar la respuesta.</p>
      @if (r.issues.length === 0) {
        <p class="empty">Todo en orden: todas las preguntas permitidas ya salen en el juego.</p>
      } @else {
        <ul class="list">
          @for (i of visibleIssues(); track i.questionId) {
            <li class="row">
              <div class="row-main">
                <p class="row-title">{{ i.text }}</p>
                <span class="meta">#{{ i.questionId }} · {{ i.examName }}</span>
              </div>
              <span class="tag reason">{{ i.reason }}</span>
              <a class="edit" [routerLink]="['/admin/questions', i.questionId]">Editar</a>
            </li>
          }
        </ul>
        @if (r.issues.length > visibleIssues().length) {
          <div class="actions" style="justify-content: center; margin-top: 0.75rem">
            <ui-button type="button" variant="ghost" (click)="showAll.set(true)">Ver todas ({{ r.issues.length }})</ui-button>
          </div>
        }
      }
    }

    <h2>Imágenes en cursos y bancos</h2>
    <p class="hint">Las señales de los cursos y de los bancos usan las imágenes de tus exámenes de señales (creados por el administrador). Cada imagen se asigna a la señal cuyo nombre coincide con la respuesta correcta.</p>
    @if (images(); as im) {
      <div class="stats">
        <div class="stat good"><strong>{{ im.matched.length }}</strong><span>señales con tu imagen</span></div>
        <div class="stat" [class.warn]="im.unmatched.length > 0"><strong>{{ im.unmatched.length }}</strong><span>preguntas sin señal asignada</span></div>
        <div class="stat"><strong>{{ im.missingCodes.length }}</strong><span>señales aún con dibujo genérico</span></div>
      </div>
      <p class="meta">Exámenes usados: {{ im.exams.length ? im.exams.join(', ') : 'ninguno' }}</p>
      @if (im.unmatched.length) {
        <p class="hint">Estas respuestas no coinciden con ningún nombre del catálogo. Escribe el nombre oficial de la señal (por ejemplo «Fin de la prohibición de adelantar») o su código (por ejemplo «SR-26A»).</p>
        <ul class="list">
          @for (u of im.unmatched; track u.questionId) {
            <li class="row">
              <img class="thumb" [src]="u.imageUrl" alt="" loading="lazy" />
              <div class="row-main">
                <p class="row-title">{{ u.answer }}</p>
                <span class="meta">#{{ u.questionId }} · {{ u.examName }}</span>
              </div>
              <a class="edit" [routerLink]="['/admin/questions', u.questionId]">Editar</a>
            </li>
          }
        </ul>
      }
      @if (im.missingCodes.length) {
        <p class="meta" style="margin-top: 0.8rem">Sin imagen: {{ im.missingCodes.join(', ') }}</p>
      }
    }
  `
})
export class AdminSignsGamePage implements OnInit {
  private readonly api = inject(PlayApi);

  readonly report = signal<SignsReport | null>(null);
  readonly images = signal<SignImageReport | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly showAll = signal(false);
  readonly visibleIssues = computed(() => {
    const issues = this.report()?.issues ?? [];
    return this.showAll() ? issues : issues.slice(0, 30);
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.signImages(true).subscribe({
      next: (r) => this.images.set(r),
      error: (err) => this.error.set(mapApiError(err))
    });
    this.api.signsReport().subscribe({
      next: (r) => {
        this.report.set(r);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  percent(part: number, total: number): number {
    return total > 0 ? Math.round((part / total) * 100) : 0;
  }
}
