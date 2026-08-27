import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
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
      subtitle="Grupos a los que perteneces y actividades pendientes del aula." />

    <ui-error [message]="error()" />

    @if (loading()) {
      <ui-loading />
    } @else {
      <section class="panel join-panel">
        <h2>Unirme a un grupo</h2>
        <form class="join" (ngSubmit)="join()">
          <label>
            Código del grupo
            <input class="input" [(ngModel)]="code" name="code" placeholder="CALE-XXXXXXXX" />
          </label>
          <ui-button type="submit">Unirme</ui-button>
        </form>
      </section>

      <section class="panel">
        <h2>Mis grupos</h2>
        @if (!groups().length) {
          <ui-empty
            title="Sin grupos"
            message="Pide el código a tu instructor o escuela e introdúcelo arriba." />
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
    .panel h2 { margin: 0 0 0.85rem; font-size: 1.05rem; }
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

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly groups = signal<GroupDto[]>([]);
  readonly pending = signal<ActivityDto[]>([]);
  readonly statusLabel = itemStatusLabel;
  code = '';

  ngOnInit(): void {
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
