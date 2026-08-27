import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
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
  lastLoginAt?: string | null;
}

interface SchoolProfileDto {
  teachersUsed: number;
  teachersMax: number;
  studentsUsed: number;
  studentsMax: number;
  planLabel: string;
}

interface SchoolJoinRequestDto {
  id: number;
  teacherUserId: number;
  teacherName: string;
  teacherEmail: string;
  schoolUserId: number;
  schoolLegalName: string;
  schoolTaxId: string;
  status: string;
  message?: string | null;
  rejectionReason?: string | null;
  createdAt: string;
  decidedAt?: string | null;
}

@Component({
  selector: 'app-school-users-page',
  standalone: true,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
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
    .hint, .muted {
      color: var(--color-text-secondary);
      margin: 0 0 0.75rem;
      font-size: var(--text-sm);
    }
    .grid-3 {
      display: grid;
      gap: 1rem;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
    }
  `],
  template: `
    <ui-page-header
      eyebrow="Escuela"
      title="Instructores y estudiantes"
      subtitle="Puedes crear y editar nombre/correo. Activar, desactivar o eliminar solo lo hace el administrador." />

    <div class="row-actions" style="justify-content: flex-start; margin-bottom: 1rem;">
      <a routerLink="/school/import">
        <ui-button type="button" variant="secondary">Importar CSV</ui-button>
      </a>
    </div>

    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />

    @if (loading()) {
      <ui-loading />
    } @else {
      <div class="grid-stats">
        <ui-stat
          label="Instructores"
          [value]="(profile()?.teachersUsed ?? 0) + ' / ' + (profile()?.teachersMax ?? 0)"
          tone="primary" />
        <ui-stat
          label="Estudiantes"
          [value]="(profile()?.studentsUsed ?? 0) + ' / ' + (profile()?.studentsMax ?? 0)"
          tone="success" />
        <ui-stat label="Plan" [value]="profile()?.planLabel || '—'" />
      </div>

      @if (joinRequests().length) {
        <ui-card>
          <h2>Solicitudes de instructores</h2>
          <p class="hint">
            Instructores que pidieron unirse con tu NIT o correo. Acepta o rechaza cada solicitud.
          </p>
          <div class="table-wrap">
            <table class="data">
              <thead>
                <tr>
                  <th>Instructor</th>
                  <th>Correo</th>
                  <th>Mensaje</th>
                  <th>Fecha</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (req of joinRequests(); track req.id) {
                  <tr>
                    <td>{{ req.teacherName }}</td>
                    <td>{{ req.teacherEmail }}</td>
                    <td>{{ req.message || '—' }}</td>
                    <td>{{ req.createdAt | date:'short' }}</td>
                    <td>
                      <div class="row-actions">
                        <ui-button
                          type="button"
                          [loading]="decidingId() === req.id"
                          (click)="acceptJoin(req.id)">
                          Aceptar
                        </ui-button>
                        <ui-button
                          type="button"
                          variant="secondary"
                          [disabled]="decidingId() === req.id"
                          (click)="rejectJoin(req.id)">
                          Rechazar
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

      <div class="grid-3">
        <ui-card>
          <h2>Crear cuenta nueva</h2>
          <p class="hint">Para personas que aún no tienen usuario en CALE.</p>
          <form class="stack" [formGroup]="form" (ngSubmit)="create()">
            <label class="field">
              Tipo
              <select formControlName="role">
                <option value="Teacher">Instructor</option>
                <option value="Student">Estudiante</option>
              </select>
            </label>
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
            <ui-button type="submit" [loading]="saving()">Crear</ui-button>
          </form>
        </ui-card>

        <ui-card>
          <h2>Vincular cuenta existente</h2>
          <p class="hint">
            Si el instructor o estudiante ya se registró solo, agrégalo con su correo.
            Debe coincidir el tipo (instructor/estudiante) y no pertenecer a otra escuela.
          </p>
          <form class="stack" [formGroup]="attachForm" (ngSubmit)="attach()">
            <label class="field">
              Tipo
              <select formControlName="role">
                <option value="Teacher">Instructor</option>
                <option value="Student">Estudiante</option>
              </select>
            </label>
            <label class="field">
              Correo de la cuenta
              <input type="email" formControlName="email" autocomplete="email"
                placeholder="instructor&#64;ejemplo.com" />
            </label>
            <ui-button type="submit" [loading]="attaching()">Vincular a mi escuela</ui-button>
          </form>
        </ui-card>

        <ui-card>
          <h2>Buscar en tu escuela</h2>
          <label class="field">
            Nombre, correo o rol
            <input
              class="input"
              [value]="query()"
              (input)="query.set($any($event.target).value)"
              placeholder="Ej. instructor, estudiante..." />
          </label>
          <p class="muted">Mostrando {{ filtered().length }} de {{ items().length }}.</p>
        </ui-card>
      </div>

      @if (editing()) {
        <ui-card>
          <h2>Editar #{{ editing()!.id }}</h2>
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
              Nueva contraseña (opcional)
              <input type="password" formControlName="newPassword" autocomplete="new-password" />
            </label>
            <div class="row">
              <ui-button type="submit" [loading]="savingEdit()">Guardar</ui-button>
              <ui-button type="button" variant="secondary" (click)="cancelEdit()">Cancelar</ui-button>
            </div>
          </form>
        </ui-card>
      }

      @if (!filtered().length) {
        <ui-empty
          title="Sin miembros"
          message="Crea una cuenta nueva o vincula un instructor/estudiante existente." />
      } @else {
        <ui-card>
          <div class="table-wrap">
            <table class="data">
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Correo</th>
                  <th>Rol</th>
                  <th>Último acceso</th>
                  <th>Estado</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (user of filtered(); track user.id) {
                  <tr>
                    <td>{{ user.name }}</td>
                    <td>{{ user.email }}</td>
                    <td>
                      <ui-badge [tone]="user.role === 'Teacher' ? 'warning' : 'neutral'">
                        {{ roleLabel(user.role) }}
                      </ui-badge>
                    </td>
                    <td>
                      {{ user.lastLoginAt ? (user.lastLoginAt | date:'short') : 'Sin acceso' }}
                    </td>
                    <td>
                      <ui-badge [tone]="user.isActive ? 'success' : 'danger'">
                        {{ user.isActive ? 'Activo' : 'Inactivo' }}
                      </ui-badge>
                    </td>
                    <td>
                      <div class="row-actions">
                        <ui-button type="button" variant="ghost" (click)="startEdit(user)">Editar</ui-button>
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
export class SchoolUsersPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);

  readonly roleLabel = roleLabel;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly attaching = signal(false);
  readonly savingEdit = signal(false);
  readonly decidingId = signal<number | null>(null);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly items = signal<UserRow[]>([]);
  readonly joinRequests = signal<SchoolJoinRequestDto[]>([]);
  readonly profile = signal<SchoolProfileDto | null>(null);
  readonly query = signal('');
  readonly editing = signal<UserRow | null>(null);

  readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const rows = this.items();
    if (!q) return rows;
    return rows.filter((u) =>
      u.name.toLowerCase().includes(q)
      || u.email.toLowerCase().includes(q)
      || roleLabel(u.role).toLowerCase().includes(q)
    );
  });

  readonly form = this.fb.nonNullable.group({
    role: ['Teacher', Validators.required],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]]
  });

  readonly attachForm = this.fb.nonNullable.group({
    role: ['Teacher', Validators.required],
    email: ['', [Validators.required, Validators.email]]
  });

  readonly editForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    newPassword: ['']
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.http.get<SchoolProfileDto>(`${env.apiUrl}/api/school/profile`).subscribe({
      next: (profile) => this.profile.set(profile),
      error: (err) => this.error.set(mapApiError(err))
    });
    this.http.get<SchoolJoinRequestDto[]>(`${env.apiUrl}/api/school/join-requests`).subscribe({
      next: (rows) => this.joinRequests.set(rows),
      error: () => this.joinRequests.set([])
    });
    this.http.get<UserRow[]>(`${env.apiUrl}/api/school/members`).subscribe({
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

  acceptJoin(id: number): void {
    this.decidingId.set(id);
    this.error.set(null);
    this.ok.set(null);
    this.http.post<SchoolJoinRequestDto>(
      `${env.apiUrl}/api/school/join-requests/${id}/accept`,
      {}
    ).subscribe({
      next: (req) => {
        this.joinRequests.update((rows) => rows.filter((r) => r.id !== id));
        this.decidingId.set(null);
        this.ok.set(`${req.teacherName} aceptado como instructor.`);
        this.reload();
      },
      error: (err) => {
        this.decidingId.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  rejectJoin(id: number): void {
    const reason = window.prompt('Motivo del rechazo (opcional):') ?? undefined;
    if (reason === undefined) {
      return;
    }
    this.decidingId.set(id);
    this.error.set(null);
    this.ok.set(null);
    this.http.post<SchoolJoinRequestDto>(
      `${env.apiUrl}/api/school/join-requests/${id}/reject`,
      { reason: reason.trim() || null }
    ).subscribe({
      next: (req) => {
        this.joinRequests.update((rows) => rows.filter((r) => r.id !== id));
        this.decidingId.set(null);
        this.ok.set(`Solicitud de ${req.teacherName} rechazada.`);
      },
      error: (err) => {
        this.decidingId.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  create(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.ok.set(null);
    this.http.post<UserRow>(`${env.apiUrl}/api/school/members`, this.form.getRawValue())
      .subscribe({
        next: (created) => {
          this.items.update((rows) => [created, ...rows]);
          this.form.patchValue({ name: '', email: '', password: '' });
          this.saving.set(false);
          this.ok.set(`${roleLabel(created.role)} ${created.name} creado.`);
          this.refreshSeats();
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }

  attach(): void {
    if (this.attachForm.invalid) {
      this.attachForm.markAllAsTouched();
      return;
    }
    this.attaching.set(true);
    this.error.set(null);
    this.ok.set(null);
    this.http.post<UserRow>(
      `${env.apiUrl}/api/school/members/attach`,
      this.attachForm.getRawValue()
    ).subscribe({
      next: (linked) => {
        this.items.update((rows) =>
          rows.some((r) => r.id === linked.id) ? rows : [linked, ...rows]
        );
        this.attachForm.patchValue({ email: '' });
        this.attaching.set(false);
        this.ok.set(`${roleLabel(linked.role)} ${linked.name} vinculado a tu escuela.`);
        this.refreshSeats();
      },
      error: (err) => {
        this.attaching.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  startEdit(user: UserRow): void {
    this.editing.set(user);
    this.editForm.reset({
      name: user.name,
      email: user.email,
      newPassword: ''
    });
  }

  cancelEdit(): void {
    this.editing.set(null);
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
    this.http.put<UserRow>(`${env.apiUrl}/api/school/members/${current.id}`, {
      name: raw.name,
      email: raw.email,
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

  private refreshSeats(): void {
    this.http.get<SchoolProfileDto>(`${env.apiUrl}/api/school/profile`).subscribe({
      next: (profile) => this.profile.set(profile)
    });
  }
}
