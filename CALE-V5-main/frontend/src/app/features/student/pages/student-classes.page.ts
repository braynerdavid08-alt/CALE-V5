import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { SessionStore } from '../../../core/auth/session.store';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { itemStatusLabel } from '../../../shared/utils/item-status-label';
import { ActivityDto, GroupDto, StudentApi } from '../api/student.api';

@Component({
  selector: 'app-student-classes-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Estudiante"
      title="Mis clases"
      subtitle="Grupos de tu escuela y actividades pendientes del aula." />

    <ui-error [message]="error()" />

    @if (loading()) {
      <ui-loading />
    } @else {
      @if (!hasSchool()) {
        <section class="panel warn-panel">
          <h2>Necesitas una escuela</h2>
          <p class="lead">
            Solo los estudiantes vinculados a una escuela pueden unirse a grupos.
            Ve a tu perfil y solicita unirte con el NIT o correo de tu CEA.
          </p>
          <a routerLink="/profile"><ui-button type="button">Ir a Perfil</ui-button></a>
        </section>
      } @else {
        <section class="panel join-panel">
          <h2>Unirme a un grupo</h2>
          <p class="lead">Usa el código que te dio tu instructor. Debe ser de tu misma escuela.</p>
          <form class="join" (ngSubmit)="join()">
            <label>
              Código del grupo
              <input class="input" [(ngModel)]="code" name="code" placeholder="CALE-XXXXXXXX" />
            </label>
            <ui-button type="submit">Unirme</ui-button>
          </form>
        </section>
      }

      <section class="panel">
        <h2>Mis grupos</h2>
        @if (!groups().length) {
          <ui-empty
            title="Sin grupos"
            message="Cuando te unas con un código de tu escuela, aparecerán aquí." />
        } @else {
          <ul class="list">
            @for (g of groups(); track g.id) {
              <li>
                <a class="row" [routerLink]="['/student/group', g.id]">
                  <div>
                    <strong>{{ g.name }}</strong>
                    <p class="meta">{{ g.teacherName || 'Sin instructor' }} · {{ g.code }}</p>
                  </div>
                  <span class="link">Abrir</span>
                </a>
              </li>
            }
          </ul>
        }
      </section>

      <section class="panel">
        <h2>Actividades pendientes</h2>
        @if (!pending().length) {
          <p class="empty">No hay actividades pendientes.</p>
        } @else {
          <ul class="list">
            @for (a of pending(); track a.id) {
              <li>
                <a class="row" [routerLink]="['/student/group', a.groupId]">
                  <div>
                    <strong>{{ a.title }}</strong>
                    <p class="meta">{{ statusLabel(a.status) }}</p>
                  </div>
                  <span class="link">Ver</span>
                </a>
              </li>
            }
          </ul>
        }
      </section>
    }
  `,
  styles: [`
    .panel {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 1.1rem 1.25rem;
      margin-bottom: 1rem;
    }
    .warn-panel {
      border-color: color-mix(in srgb, var(--color-warning) 45%, var(--color-border));
      background: color-mix(in srgb, var(--color-warning) 8%, var(--color-surface));
    }
    .panel h2 { margin: 0 0 0.5rem; font-size: 1.05rem; }
    .lead {
      margin: 0 0 0.85rem;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      line-height: 1.45;
      max-width: 36rem;
    }
    .join {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      align-items: flex-end;
    }
    .join label {
      display: grid;
      gap: 0.35rem;
      flex: 1 1 16rem;
      font-size: var(--text-sm);
      color: var(--color-text-secondary);
    }
    .list { list-style: none; margin: 0; padding: 0; }
    .list li + li { border-top: 1px solid var(--color-border); }
    .row {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: center;
      padding: 0.85rem 0;
      text-decoration: none;
      color: inherit;
    }
    .meta { margin: 0.2rem 0 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .link { color: var(--color-primary); font-weight: 600; font-size: var(--text-sm); }
    .empty { margin: 0; color: var(--color-text-secondary); }
  `]
})
export class StudentClassesPage implements OnInit {
  private readonly api = inject(StudentApi);
  private readonly session = inject(SessionStore);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly groups = signal<GroupDto[]>([]);
  readonly pending = signal<ActivityDto[]>([]);
  readonly hasSchool = signal(false);
  readonly statusLabel = itemStatusLabel;
  code = '';

  ngOnInit(): void {
    this.hasSchool.set(!!this.session.user()?.schoolId);
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.dashboard().subscribe({
      next: (d) => {
        this.groups.set(d.groups);
        this.pending.set(d.pendingActivities);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  join(): void {
    if (!this.hasSchool()) {
      this.error.set('Debes estar vinculado a una escuela para unirte a un grupo.');
      return;
    }
    if (!this.code.trim()) return;
    this.api.joinGroup(this.code.trim()).subscribe({
      next: () => {
        this.code = '';
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
