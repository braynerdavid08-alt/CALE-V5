import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
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
  direction: 'Request' | 'Invite';
  memberUserId: number;
  memberName: string;
  memberEmail: string;
  memberRole: string;
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
      subtitle="Cada persona crea su propia cuenta. Tú la invitas o aceptas su solicitud; nadie queda vinculado sin aceptar." />

    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />

    @if (loading()) {
      <ui-loading />
    } @else {
      <div class="grid-stats">
        @if (session.freeAccess()) {
          <ui-stat label="Instructores" [value]="profile()?.teachersUsed ?? 0" tone="primary" />
          <ui-stat label="Estudiantes" [value]="profile()?.studentsUsed ?? 0" tone="success" />
          <ui-stat label="Cupos" value="Sin límite" />
        } @else {
          <ui-stat
            label="Instructores"
            [value]="(profile()?.teachersUsed ?? 0) + ' / ' + (profile()?.teachersMax ?? 0)"
            tone="primary" />
          <ui-stat
            label="Estudiantes"
            [value]="(profile()?.studentsUsed ?? 0) + ' / ' + (profile()?.studentsMax ?? 0)"
            tone="success" />
          <ui-stat label="Plan" [value]="profile()?.planLabel || '—'" />
        }
      </div>

      @if (incoming().length) {
        <ui-card>
          <h2>Solicitudes para unirse</h2>
          <p class="hint">
            Instructores y estudiantes que pidieron unirse con tu NIT o correo. Acepta o rechaza cada solicitud.
          </p>
          <div class="table-wrap">
            <table class="data">
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Correo</th>
                  <th>Rol</th>
                  <th>Mensaje</th>
                  <th>Fecha</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (req of incoming(); track req.id) {
                  <tr>
                    <td data-label="Nombre">{{ req.memberName }}</td>
                    <td data-label="Correo">{{ req.memberEmail }}</td>
                    <td data-label="Rol">{{ roleLabel(req.memberRole) }}</td>
                    <td data-label="Mensaje">{{ req.message || '—' }}</td>
                    <td data-label="Fecha">{{ req.createdAt | date:'short' }}</td>
                    <td>
                      <div class="row-actions">
                        <ui-button
                          type="button"
                          [loading]="decidingId() === req.id"
                          (click)="acceptJoin(req)">
                          Aceptar
                        </ui-button>
                        <ui-button
                          type="button"
                          variant="secondary"
                          [disabled]="decidingId() === req.id"
                          (click)="rejectJoin(req)">
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
          <h2>Invitar a tu escuela</h2>
          <p class="hint">
            La persona debe tener su propia cuenta en Luz Verde y no pertenecer a otra escuela.
            Recibirá la invitación en su perfil y quedará vinculada solo cuando la acepte.
          </p>
          <form class="stack" [formGroup]="inviteForm" (ngSubmit)="invite()">
            <label class="field">
              Tipo
              <select formControlName="role">
                <option value="Student">Estudiante</option>
                <option value="Teacher">Instructor</option>
              </select>
            </label>
            <label class="field">
              Correo de su cuenta
              <input type="email" formControlName="email" autocomplete="off"
                placeholder="persona&#64;ejemplo.com" />
            </label>
            <label class="field">
              Mensaje (opcional)
              <input formControlName="message" maxlength="500" />
            </label>
            <ui-button type="submit" [loading]="inviting()">Enviar invitación</ui-button>
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

      @if (sentInvites().length) {
        <ui-card>
          <h2>Invitaciones enviadas</h2>
          <p class="hint">Pendientes de que la persona las acepte.</p>
          <div class="table-wrap">
            <table class="data">
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Correo</th>
                  <th>Rol</th>
                  <th>Fecha</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (inv of sentInvites(); track inv.id) {
                  <tr>
                    <td data-label="Nombre">{{ inv.memberName }}</td>
                    <td data-label="Correo">{{ inv.memberEmail }}</td>
                    <td data-label="Rol">{{ roleLabel(inv.memberRole) }}</td>
                    <td data-label="Fecha">{{ inv.createdAt | date:'short' }}</td>
                    <td>
                      <div class="row-actions">
                        <ui-button
                          type="button"
                          variant="secondary"
                          [loading]="decidingId() === inv.id"
                          (click)="cancelInvite(inv)">
                          Cancelar
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

      @if (editing()) {
        <ui-card>
          <h2>Editar #{{ editing()!.id }}</h2>
          <p class="hint">
            El correo de acceso no se puede cambiar. La contraseña solo se puede restablecer en cuentas que creó tu escuela antes.
          </p>
          <form class="stack" [formGroup]="editForm" (ngSubmit)="saveEdit()">
            <label class="field">
              Nombre
              <input formControlName="name" />
            </label>
            <label class="field">
              Correo
              <input type="email" [value]="editing()!.email" disabled />
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
          message="Invita a instructores o estudiantes con el correo de su cuenta, o acepta sus solicitudes." />
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
                    <td data-label="Nombre">{{ user.name }}</td>
                    <td data-label="Correo">{{ user.email }}</td>
                    <td data-label="Rol">
                      <ui-badge [tone]="user.role === 'Teacher' ? 'warning' : 'neutral'">
                        {{ roleLabel(user.role) }}
                      </ui-badge>
                    </td>
                    <td data-label="Último acceso">
                      {{ user.lastLoginAt ? (user.lastLoginAt | date:'short') : 'Sin acceso' }}
                    </td>
                    <td data-label="Estado">
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
  readonly session = inject(SessionStore);

  readonly roleLabel = roleLabel;
  readonly loading = signal(true);
  readonly inviting = signal(false);
  readonly savingEdit = signal(false);
  readonly decidingId = signal<number | null>(null);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly items = signal<UserRow[]>([]);
  readonly joinRequests = signal<SchoolJoinRequestDto[]>([]);
  readonly profile = signal<SchoolProfileDto | null>(null);
  readonly query = signal('');
  readonly editing = signal<UserRow | null>(null);

  readonly incoming = computed(() => this.joinRequests().filter((r) => r.direction === 'Request'));
  readonly sentInvites = computed(() => this.joinRequests().filter((r) => r.direction === 'Invite'));

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

  readonly inviteForm = this.fb.nonNullable.group({
    role: ['Student', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    message: ['', Validators.maxLength(500)]
  });

  readonly editForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    newPassword: ['']
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.refreshSeats();
    this.loadJoinRequests();
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

  invite(): void {
    if (this.inviteForm.invalid) {
      this.inviteForm.markAllAsTouched();
      return;
    }
    this.inviting.set(true);
    this.error.set(null);
    this.ok.set(null);
    const raw = this.inviteForm.getRawValue();
    this.http.post<{ message: string }>(`${env.apiUrl}/api/school/invitations`, {
      email: raw.email.trim(),
      role: raw.role,
      message: raw.message.trim() || null
    }).subscribe({
      next: (res) => {
        this.inviting.set(false);
        this.inviteForm.patchValue({ email: '', message: '' });
        this.ok.set(res.message);
        this.reload();
      },
      error: (err) => {
        this.inviting.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  cancelInvite(inv: SchoolJoinRequestDto): void {
    this.decidingId.set(inv.id);
    this.error.set(null);
    this.ok.set(null);
    this.http.post(`${env.apiUrl}/api/school/invitations/${inv.id}/cancel`, {}).subscribe({
      next: () => {
        this.decidingId.set(null);
        this.joinRequests.update((rows) => rows.filter((r) => r.id !== inv.id));
        this.ok.set(`Invitación a ${inv.memberName} cancelada.`);
      },
      error: (err) => {
        this.decidingId.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  acceptJoin(req: SchoolJoinRequestDto): void {
    this.decidingId.set(req.id);
    this.error.set(null);
    this.ok.set(null);
    this.http.post<SchoolJoinRequestDto>(
      `${env.apiUrl}/api/school/join-requests/${req.id}/accept`,
      {}
    ).subscribe({
      next: () => {
        this.decidingId.set(null);
        this.ok.set(`${req.memberName} se unió a tu escuela como ${roleLabel(req.memberRole).toLowerCase()}.`);
        this.reload();
      },
      error: (err) => {
        this.decidingId.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  rejectJoin(req: SchoolJoinRequestDto): void {
    const reason = window.prompt('Motivo del rechazo (opcional):') ?? undefined;
    if (reason === undefined) {
      return;
    }
    this.decidingId.set(req.id);
    this.error.set(null);
    this.ok.set(null);
    this.http.post<SchoolJoinRequestDto>(
      `${env.apiUrl}/api/school/join-requests/${req.id}/reject`,
      { reason: reason.trim() || null }
    ).subscribe({
      next: () => {
        this.joinRequests.update((rows) => rows.filter((r) => r.id !== req.id));
        this.decidingId.set(null);
        this.ok.set(`Solicitud de ${req.memberName} rechazada.`);
      },
      error: (err) => {
        this.decidingId.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }

  startEdit(user: UserRow): void {
    this.editing.set(user);
    this.editForm.reset({ name: user.name, newPassword: '' });
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

  private loadJoinRequests(): void {
    this.http.get<SchoolJoinRequestDto[]>(`${env.apiUrl}/api/school/join-requests`).subscribe({
      next: (rows) => this.joinRequests.set(rows),
      error: () => this.joinRequests.set([])
    });
  }

  private refreshSeats(): void {
    this.http.get<SchoolProfileDto>(`${env.apiUrl}/api/school/profile`).subscribe({
      next: (profile) => this.profile.set(profile),
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
