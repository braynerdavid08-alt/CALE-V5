import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { mapApiError } from '../../../core/http/map-api-error';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { roleLabel } from '../../../shared/utils/role-label';
import { AdminInsightsApi, QuestionAccuracy, StorageReport, WeeklySummary } from '../api/admin-insights.api';

/** Render's free Postgres plan; used only to show how full the database is. */
const DB_LIMIT_BYTES = 1024 * 1024 * 1024;

@Component({
  selector: 'app-admin-usage-page',
  standalone: true,
  imports: [DatePipe, RouterLink, UiButtonComponent, UiErrorComponent, UiLoadingComponent, UiPageHeaderComponent],
  styles: [`
    :host { display: block; }
    .top { display: flex; flex-wrap: wrap; gap: 0.5rem; justify-content: space-between; align-items: center; margin-bottom: 0.5rem; }
    .range { display: flex; gap: 0.35rem; }
    .range button {
      padding: 0.45rem 0.85rem;
      border-radius: 999px;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }
    .range button.on { background: var(--color-primary-soft); color: var(--color-primary); border-color: transparent; }
    h2 { margin: 1.6rem 0 0.4rem; font-size: var(--text-lg); }
    .hint { margin: 0 0 0.8rem; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .stats { display: grid; grid-template-columns: repeat(auto-fill, minmax(10.5rem, 1fr)); gap: 0.7rem; }
    .stat { padding: 0.95rem 1rem; border-radius: 1rem; border: 1px solid var(--color-border); background: var(--color-surface); }
    .stat strong { display: block; font-size: 1.7rem; line-height: 1.1; }
    .stat span { color: var(--color-text-secondary); font-size: var(--text-sm); }
    .stat small { display: block; margin-top: 0.3rem; color: var(--color-text-secondary); font-size: var(--text-xs); }
    .stat.warn strong { color: #b45309; }
    .stat.link { text-decoration: none; color: inherit; }
    .stat.link:hover { border-color: var(--color-primary); }
    .meter { height: 0.6rem; border-radius: 999px; background: var(--color-surface-raised); overflow: hidden; margin: 0.4rem 0 0.2rem; }
    .meter > i { display: block; height: 100%; background: var(--color-primary); }
    .meter > i.high { background: #dc2626; }
    .list { display: grid; gap: 0.55rem; margin: 0; padding: 0; list-style: none; }
    .row {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem 1rem;
      align-items: center;
      justify-content: space-between;
      padding: 0.8rem 1rem;
      border-radius: 0.9rem;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
    }
    .row-main { display: grid; gap: 0.2rem; min-width: 0; flex: 1 1 15rem; }
    .row-title { margin: 0; font-weight: 700; overflow-wrap: anywhere; }
    .meta { color: var(--color-text-secondary); font-size: var(--text-sm); }
    .pct { font-weight: 800; font-size: var(--text-lg); min-width: 3.5rem; text-align: right; }
    .pct.bad { color: #dc2626; }
    .pct.mid { color: #b45309; }
    .edit { padding: 0.45rem 0.9rem; border-radius: 999px; background: var(--color-primary-soft); color: var(--color-primary); font-weight: 800; text-decoration: none; white-space: nowrap; }
    .thumb { width: 3rem; height: 3rem; object-fit: cover; border-radius: 0.5rem; background: var(--color-surface-raised); }
    .empty { text-align: center; padding: 1.1rem; color: var(--color-text-secondary); border: 1px dashed var(--color-border); border-radius: 1rem; }
    @media (max-width: 640px) {
      .stats { grid-template-columns: 1fr 1fr; }
      .edit { flex: 1; text-align: center; }
    }
  `],
  template: `
    <ui-page-header
      eyebrow="Reportes"
      title="Uso de la app"
      subtitle="Actividad reciente, espacio que ocupan las imágenes y preguntas donde más se equivocan los estudiantes. Cada lunes te llega este resumen como notificación." />
    @if (error()) { <ui-error [message]="error()" /> }

    <div class="top">
      <div class="range" role="group" aria-label="Periodo">
        @for (d of [7, 30]; track d) {
          <button type="button" [class.on]="days() === d" (click)="setDays(d)">Últimos {{ d }} días</button>
        }
      </div>
      <ui-button type="button" variant="ghost" [loading]="loading()" (click)="load()">Actualizar</ui-button>
    </div>

    @if (loading() && !weekly()) {
      <ui-loading />
    }

    @if (weekly(); as w) {
      <p class="hint">Del {{ w.from | date:'d MMM' }} al {{ w.to | date:'d MMM y' }}</p>
      <div class="stats">
        <div class="stat">
          <strong>{{ w.newUsers }}</strong><span>usuarios nuevos</span>
          @if (w.newUsersByRole.length) {
            <small>
              @for (r of w.newUsersByRole; track r.role; let last = $last) { {{ r.count }} {{ role(r.role) }}{{ last ? '' : ' · ' }} }
            </small>
          }
        </div>
        <div class="stat"><strong>{{ w.activeStudents }}</strong><span>usuarios activos (exámenes o juegos)</span></div>
        <div class="stat"><strong>{{ w.examsFinished }}</strong><span>exámenes terminados</span></div>
        <div class="stat"><strong>{{ w.gamesPlayed }}</strong><span>partidas de juegos</span></div>
        <div class="stat"><strong>{{ w.newRequests }}</strong><span>solicitudes recibidas</span></div>
        <a class="stat link" [class.warn]="w.pendingRequests > 0" routerLink="/admin/requests">
          <strong>{{ w.pendingRequests }}</strong><span>pendientes por revisar</span>
          @if (w.pendingReports) { <small>{{ w.pendingReports }} reportes de preguntas</small> }
        </a>
      </div>
    }

    @if (storage(); as s) {
      <h2>Almacenamiento de imágenes</h2>
      <p class="hint">Las imágenes se guardan en la base de datos. Desde ahora se reducen al subirlas.</p>
      <div class="stats">
        <div class="stat"><strong>{{ size(s.bytes) }}</strong><span>en {{ s.files }} {{ s.files === 1 ? 'imagen' : 'imágenes' }}</span></div>
        @if (s.databaseBytes !== null) {
          <div class="stat" [class.warn]="dbPercent(s.databaseBytes) >= 80">
            <strong>{{ size(s.databaseBytes) }}</strong><span>base de datos completa</span>
            <div class="meter" aria-hidden="true"><i [class.high]="dbPercent(s.databaseBytes) >= 80" [style.width.%]="dbPercent(s.databaseBytes)"></i></div>
            <small>{{ dbPercent(s.databaseBytes) }}% de 1 GB (plan gratuito de Render)</small>
          </div>
        }
        @for (o of s.byOwner; track o.group) {
          <div class="stat"><strong>{{ size(o.bytes) }}</strong><span>{{ o.group }} · {{ o.files }} {{ o.files === 1 ? 'imagen' : 'imágenes' }}</span></div>
        }
      </div>
      @if (s.largest.length) {
        <h2 style="font-size: var(--text-base)">Imágenes más pesadas</h2>
        <ul class="list">
          @for (f of s.largest; track f.id) {
            <li class="row">
              <img class="thumb" [src]="mediaUrl(f.id)" alt="" loading="lazy" />
              <div class="row-main">
                <p class="row-title">{{ size(f.bytes) }} · {{ f.contentType }}</p>
                <span class="meta">{{ f.owner }} · {{ f.createdAt | date:'d MMM y' }}</span>
              </div>
            </li>
          }
        </ul>
      }
    }

    @if (hardest(); as list) {
      <h2>Preguntas con más errores</h2>
      <p class="hint">Porcentaje de aciertos en exámenes (mínimo 8 respuestas). Si casi nadie acierta, revisa que la respuesta correcta esté bien marcada.</p>
      @if (!list.length) {
        <p class="empty">Aún no hay suficientes respuestas para calcularlo.</p>
      } @else {
        <ul class="list">
          @for (q of list; track q.questionId) {
            <li class="row">
              <div class="row-main">
                <p class="row-title">{{ q.text }}</p>
                <span class="meta">#{{ q.questionId }} · {{ q.bankName }} · {{ q.answers }} respuestas</span>
              </div>
              <span class="pct" [class.bad]="q.percent < 35" [class.mid]="q.percent >= 35 && q.percent < 55">{{ q.percent }}%</span>
              <a class="edit" [routerLink]="['/admin/questions', q.questionId]">Revisar</a>
            </li>
          }
        </ul>
      }
    }
  `
})
export class AdminUsagePage implements OnInit {
  private readonly api = inject(AdminInsightsApi);

  readonly days = signal(7);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly weekly = signal<WeeklySummary | null>(null);
  readonly storage = signal<StorageReport | null>(null);
  readonly hardest = signal<QuestionAccuracy[] | null>(null);

  ngOnInit(): void {
    this.load();
  }

  setDays(d: number): void {
    this.days.set(d);
    this.api.weekly(d).subscribe({ next: (w) => this.weekly.set(w), error: (e) => this.error.set(mapApiError(e)) });
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    forkJoin({
      weekly: this.api.weekly(this.days()),
      storage: this.api.storage(),
      hardest: this.api.hardestQuestions(30)
    }).subscribe({
      next: (r) => {
        this.weekly.set(r.weekly);
        this.storage.set(r.storage);
        this.hardest.set(r.hardest);
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(mapApiError(e));
        this.loading.set(false);
      }
    });
  }

  role(r: string): string {
    return roleLabel(r).toLowerCase();
  }

  size(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
    return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
  }

  dbPercent(bytes: number): number {
    return Math.min(100, Math.round((bytes / DB_LIMIT_BYTES) * 100));
  }

  mediaUrl(id: string): string {
    return resolveMediaUrl(`/api/media/${id}`);
  }
}
