import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiStatComponent } from '../../../shared/ui/ui-stat.component';
import { roleLabel } from '../../../shared/utils/role-label';

interface UserRow {
  id: number;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
  createdAt: string;
}

@Component({
  selector: 'app-admin-role-users-page',
  standalone: true,
  imports: [
    RouterLink,
    DatePipe,
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
      [title]="pageTitle()"
      [subtitle]="pageSubtitle()" />

    <ui-error [message]="error()" />

    @if (loading()) {
      <ui-loading />
    } @else {
      <div class="grid-stats">
        <ui-stat label="Total" [value]="filtered().length" tone="primary" />
        <ui-stat label="Activos" [value]="activeCount()" tone="success" />
        <ui-stat label="Inactivos" [value]="inactiveCount()" />
      </div>

      <ui-card>
        <div class="toolbar">
          <input
            type="search"
            class="search"
            [value]="query()"
            (input)="query.set($any($event.target).value)"
            placeholder="Buscar por nombre o correo…" />
          <a routerLink="/admin/users">
            <ui-button type="button" variant="secondary">Gestionar en Usuarios</ui-button>
          </a>
        </div>

        @if (filtered().length === 0) {
          <ui-empty
            title="Sin resultados"
            [message]="'No hay ' + roleLabel(roleFilter()) + ' que coincidan.'" />
        } @else {
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Correo</th>
                  <th>Estado</th>
                  <th>Alta</th>
                </tr>
              </thead>
              <tbody>
                @for (u of filtered(); track u.id) {
                  <tr>
                    <td>{{ u.name }}</td>
                    <td>{{ u.email }}</td>
                    <td>
                      <ui-badge [tone]="u.isActive ? 'success' : 'neutral'">
                        {{ u.isActive ? 'Activo' : 'Inactivo' }}
                      </ui-badge>
                    </td>
                    <td>{{ u.createdAt | date: 'mediumDate' }}</td>
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
    }
    th { color: var(--color-text-secondary); font-weight: 600; }
  `]
})
export class AdminRoleUsersPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  readonly roleFilter = signal('Teacher');
  readonly pageTitle = signal('Instructores');
  readonly pageSubtitle = signal('');
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<UserRow[]>([]);
  readonly query = signal('');

  readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const role = this.roleFilter();
    let rows = this.items().filter((u) => u.role === role);
    if (!q) {
      return rows;
    }
    return rows.filter(
      (u) =>
        u.name.toLowerCase().includes(q) ||
        u.email.toLowerCase().includes(q)
    );
  });

  readonly activeCount = computed(() => this.filtered().filter((u) => u.isActive).length);
  readonly inactiveCount = computed(() => this.filtered().filter((u) => !u.isActive).length);

  readonly roleLabel = roleLabel;

  ngOnInit(): void {
    const data = this.route.snapshot.data;
    this.roleFilter.set((data['roleFilter'] as string) ?? 'Teacher');
    this.pageTitle.set((data['title'] as string) ?? roleLabel(this.roleFilter()));
    this.pageSubtitle.set((data['subtitle'] as string) ?? '');

    this.http.get<UserRow[]>(`${env.apiUrl}/api/admin/users`).subscribe({
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
