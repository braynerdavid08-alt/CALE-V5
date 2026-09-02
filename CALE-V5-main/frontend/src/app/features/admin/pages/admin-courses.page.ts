import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiStatComponent } from '../../../shared/ui/ui-stat.component';
import { GroupDto } from '../../student/api/student.api';
import { TeacherApi } from '../../teacher/api/teacher.api';

@Component({
  selector: 'app-admin-courses-page',
  standalone: true,
  imports: [
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiStatComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Administración"
      title="Cursos / Grupos"
      subtitle="Grupos formativos y aulas activas en la plataforma." />

    <ui-error [message]="error()" />

    @if (loading()) {
      <ui-loading />
    } @else {
      <div class="grid-stats">
        <ui-stat label="Grupos" [value]="items().length" tone="primary" />
        <ui-stat label="Activos" [value]="activeCount()" tone="success" />
        <ui-stat label="Estudiantes" [value]="memberTotal()" />
      </div>

      <ui-card>
        <div class="toolbar">
          <input
            type="search"
            class="search"
            [value]="query()"
            (input)="query.set($any($event.target).value)"
            placeholder="Buscar grupo o instructor…" />
          <a routerLink="/teacher/groups">
            <ui-button type="button" variant="secondary">Gestionar grupos</ui-button>
          </a>
        </div>

        @if (filtered().length === 0) {
          <ui-empty title="Sin grupos" message="No hay grupos que coincidan." />
        } @else {
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Grupo</th>
                  <th>Instructor</th>
                  <th>Miembros</th>
                  <th>Código</th>
                  <th>Estado</th>
                </tr>
              </thead>
              <tbody>
                @for (g of filtered(); track g.id) {
                  <tr>
                    <td>
                      <strong>{{ g.name }}</strong>
                      @if (g.description) {
                        <div class="muted">{{ g.description }}</div>
                      }
                    </td>
                    <td>{{ g.teacherName || '—' }}</td>
                    <td>{{ g.memberCount }}</td>
                    <td><code>{{ g.code }}</code></td>
                    <td>
                      <ui-badge [tone]="g.isActive ? 'success' : 'neutral'">
                        {{ g.isActive ? 'Activo' : 'Inactivo' }}
                      </ui-badge>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </ui-card>
    }
  `,
  styles: [`
    .grid-stats {
      display: grid;
      gap: 0.75rem;
      grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
      margin-bottom: 1rem;
    }
    .toolbar {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 1rem;
    }
    .search {
      flex: 1;
      min-width: 200px;
      padding: 0.5rem 0.75rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
    }
    .table-wrap { overflow-x: auto; }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.92rem;
    }
    th, td {
      text-align: left;
      padding: 0.55rem 0.65rem;
      border-bottom: 1px solid var(--color-border);
      vertical-align: top;
    }
    th { color: var(--color-text-secondary); font-weight: 600; }
    .muted {
      font-size: 0.85rem;
      color: var(--color-text-secondary);
      margin-top: 0.15rem;
    }
  `]
})
export class AdminCoursesPage implements OnInit {
  private readonly api = inject(TeacherApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<GroupDto[]>([]);
  readonly query = signal('');

  readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    if (!q) {
      return this.items();
    }
    return this.items().filter(
      (g) =>
        g.name.toLowerCase().includes(q) ||
        (g.description ?? '').toLowerCase().includes(q) ||
        (g.teacherName ?? '').toLowerCase().includes(q)
    );
  });

  readonly activeCount = computed(() => this.items().filter((g) => g.isActive).length);
  readonly memberTotal = computed(() =>
    this.items().reduce((sum, g) => sum + (g.memberCount ?? 0), 0)
  );

  ngOnInit(): void {
    this.api.groups().subscribe({
      next: (rows) => {
        this.items.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
