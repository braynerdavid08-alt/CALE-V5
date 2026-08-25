import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { SessionStore } from '../../../core/auth/session.store';
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
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
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
  selector: 'app-admin-users-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiStatComponent,
    UiSuccessComponent
  ],
  styles: [`
    .row-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
      justify-content: flex-end;
    }
    .edit-panel {
      margin-top: var(--spacing-md);
    }
  `],
  template: `
    <ui-page-header
      eyebrow="Administración"
      title="Usuarios"
      subtitle="Crea docentes, edita datos, activa/desactiva o elimina cuentas." />

    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />

    @if (loading()) {
      <ui-loading />
    } @else {
      <div class="grid-stats">
        <ui-stat label="Total" [value]="items().length" tone="primary" />
        <ui-stat label="Estudiantes" [value]="countBy('Student')" />
        <ui-stat label="Docentes" [value]="countBy('Teacher')" />
        <ui-stat label="Escuelas" [value]="countBy('School')" />
        <ui-stat label="Admins" [value]="countBy('Admin')" />
        <ui-stat label="Activos" [value]="activeCount()" tone="success" />
      </div>

      <div class="grid-2">
        <ui-card>
          <h2>Crear docente</h2>
          <p class="hint">Alta rápida de profesores. Estudiantes y escuelas se registran solos.</p>
          <form class="stack" [formGroup]="form" (ngSubmit)="createTeacher()">
            <label class="field">
              Nombre
              <input formControlName="name" autocomplete="name" />
            </label>
            <label class="field">
              Correo
              <input type="email" formControlName="email" autocomplete="email" />
            </label>
            <label class="field">
              Contraseña temporal
              <input type="password" formControlName="password" autocomplete="new-password" />
            </label>
            <ui-button type="submit" [loading]="saving()">Crear docente</ui-button>
          </form>
        </ui-card>

        <ui-card>
          <h2>Buscar</h2>
          <label class="field">
            Nombre, correo o rol
            <input
              class="input"
              [value]="query()"
              (input)="query.set($any($event.target).value)"
              placeholder="Ej. profesor, estudiante..." />
          </label>
          <p class="muted">Mostrando {{ filtered().length }} de {{ items().length }} usuarios.</p>
        </ui-card>
      </div>

      @if (editing()) {
        <ui-card class="edit-panel">
          <h2>Editar usuario #{{ editing()!.id }}</h2>
          <form class="stack" [formGroup]="editForm" (ngSubmit)="saveEdit()">
            <label class="field">
              Nombre
              <input formControlName="name" />
            </label>
            <label class="field">
              Correo
              <input type="email" formControlName="email" />
            </label>
            <label class="field">
              Rol
              <select formControlName="role">
                <option value="Student">Estudiante</option>
                <option value="Teacher">Docente</option>
                <option value="School">Escuela</option>
                <option value="Admin">Administrador</option>
              </select>
            </label>
            <label class="field">
              Nueva contraseña (opcional)
              <input type="password" formControlName="newPassword" autocomplete="new-password" />
            </label>
            <div class="row">
              <ui-button type="submit" [loading]="savingEdit()">Guardar cambios</ui-button>
              <ui-button type="button" variant="secondary" (click)="cancelEdit()">Cancelar</ui-button>
            </div>
          </form>
        </ui-card>
      }

      @if (!filtered().length) {
        <ui-empty title="Sin usuarios" message="No hay resultados con ese filtro." />
      } @else {
        <ui-card>
          <div class="table-wrap">
            <table class="data">
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Correo</th>
                  <th>Rol</th>
                  <th>Estado</th>
                  <th>Alta</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (user of filtered(); track user.id) {
                  <tr>
                    <td>{{ user.name }}</td>
                    <td>{{ user.email }}</td>
                    <td>
                      <ui-badge [tone]="roleTone(user.role)">
                        {{ roleLabel(user.role) }}
                      </ui-badge>
                    </td>
                    <td>
                      <ui-badge [tone]="user.isActive ? 'success' : 'danger'">
                        {{ user.isActive ? 'Activo' : 'Inactivo' }}
                      </ui-badge>
                    </td>
                    <td>{{ formatDate(user.createdAt) }}</td>
                    <td>
                      <div class="row-actions">
                        <ui-button
                          type="button"
                          variant="ghost"
                          [disabled]="busyId() === user.id"
                          (click)="startEdit(user)">
                          Editar
                        </ui-button>
                        <ui-button
                          type="button"
                          variant="ghost"
                          [disabled]="busyId() === user.id || user.id === meId"
                          (click)="toggleActive(user)">
                          {{ user.isActive ? 'Desactivar' : 'Activar' }}
                        </ui-button>
                        <ui-button
                          type="button"
                          variant="ghost"
                          [disabled]="busyId() === user.id || user.id === meId"
                          (click)="deleteUser(user)">
                          Borrar
                        </ui-button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </ui-card>
      }
    }
  `
})
export class AdminUsersPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  private readonly session = inject(SessionStore);

  readonly roleLabel = roleLabel;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly savingEdit = signal(false);
  readonly busyId = signal<number | null>(null);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly items = signal<UserRow[]>([]);
  readonly query = signal('');
  readonly editing = signal<UserRow | null>(null);

  readonly meId = this.session.user()?.id ?? -1;

  readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const rows = this.items();
    if (!q) return rows;
    return rows.filter((u) =>
      u.name.toLowerCase().includes(q)
      || u.email.toLowerCase().includes(q)
      || roleLabel(u.role).toLowerCase().includes(q)
      || u.role.toLowerCase().includes(q)
    );
  });

  readonly activeCount = computed(
    () => this.items().filter((u) => u.isActive).length
  );

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]]
  });

  readonly editForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    role: ['Student', Validators.required],
    newPassword: ['']
  });

  ngOnInit(): void {
    this.reload();
  }

  countBy(role: string): number {
    return this.items().filter((u) => u.role === role).length;
  }

  roleTone(role: string): 'primary' | 'warning' | 'neutral' | 'success' {
    if (role === 'Admin') return 'primary';
    if (role === 'School') return 'success';
    if (role === 'Teacher') return 'warning';
    return 'neutral';
  }

  formatDate(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return value;
    return date.toLocaleDateString('es-ES', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
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

  createTeacher(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.ok.set(null);
    const body = this.form.getRawValue();

    this.http.post<UserRow>(`${env.apiUrl}/api/admin/users/teachers`, body)
      .subscribe({
        next: (created) => {
          this.items.update((rows) => [created, ...rows]);
          this.form.reset({ name: '', email: '', password: '' });
          this.saving.set(false);
          this.ok.set(`Docente ${created.name} creado.`);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }

  startEdit(user: UserRow): void {
    this.editing.set(user);
    this.error.set(null);
    this.ok.set(null);
    this.editForm.reset({
      name: user.name,
      email: user.email,
      role: user.role,
      newPassword: ''
    });
  }

  cancelEdit(): void {
    this.editing.set(null);
    this.editForm.reset({
      name: '',
      email: '',
      role: 'Student',
      newPassword: ''
    });
  }

  saveEdit(): void {
    const current = this.editing();
    if (!current || this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }

    const raw = this.editForm.getRawValue();
    const password = raw.newPassword.trim();
    if (password && password.length < 8) {
      this.error.set('La nueva contraseña debe tener al menos 8 caracteres.');
      return;
    }

    this.savingEdit.set(true);
    this.error.set(null);
    this.ok.set(null);

    this.http.put<UserRow>(`${env.apiUrl}/api/admin/users/${current.id}`, {
      name: raw.name,
      email: raw.email,
      role: raw.role,
      newPassword: password || null
    }).subscribe({
      next: (updated) => {
        this.items.update((rows) =>
          rows.map((row) => (row.id === updated.id ? updated : row))
        );
        this.savingEdit.set(false);
        this.ok.set(`${updated.name} actualizado.`);
        this.cancelEdit();
      },
      error: (err) => {
        this.savingEdit.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  toggleActive(user: UserRow): void {
    this.busyId.set(user.id);
    this.error.set(null);
    this.ok.set(null);

    this.http.patch<UserRow>(
      `${env.apiUrl}/api/admin/users/${user.id}/active`,
      { isActive: !user.isActive }
    ).subscribe({
      next: (updated) => {
        this.items.update((rows) =>
          rows.map((row) => (row.id === updated.id ? updated : row))
        );
        this.busyId.set(null);
        this.ok.set(
          updated.isActive
            ? `${updated.name} activado.`
            : `${updated.name} desactivado.`
        );
      },
      error: (err) => {
        this.busyId.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  deleteUser(user: UserRow): void {
    if (!confirm(`¿Borrar definitivamente a ${user.name} (${user.email})?`)) {
      return;
    }

    this.busyId.set(user.id);
    this.error.set(null);
    this.ok.set(null);

    this.http.delete(`${env.apiUrl}/api/admin/users/${user.id}`).subscribe({
      next: () => {
        this.items.update((rows) => rows.filter((row) => row.id !== user.id));
        if (this.editing()?.id === user.id) {
          this.cancelEdit();
        }
        this.busyId.set(null);
        this.ok.set(`${user.name} eliminado.`);
      },
      error: (err) => {
        this.busyId.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }
}
