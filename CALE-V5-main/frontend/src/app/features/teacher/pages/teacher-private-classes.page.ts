import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';

interface SettingsDto {
  listed: boolean;
  acceptsPrivateClasses: boolean;
}

interface RequestDto {
  id: number;
  studentName: string;
  studentEmail: string | null;
  category: string;
  preferredSchedule: string;
  message: string;
  status: string;
  reply: string | null;
  createdAt: string;
}

@Component({
  selector: 'app-teacher-private-classes-page',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiCardComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiSuccessComponent
  ],
  template: `
    <div class="page">
      <header>
        <p class="eyebrow">Clases particulares</p>
        <h1>Solicitudes de estudiantes</h1>
        <p class="muted">
          Luz Verde solo te conecta con el estudiante. El acuerdo y el pago son entre ustedes.
        </p>
      </header>

      <ui-error [message]="error()" />
      <ui-success [message]="ok()" />

      @if (loading()) {
        <ui-loading />
      } @else {
        <ui-card>
          <h2>Recibir solicitudes</h2>
          @if (!settings()?.listed) {
            <p class="muted">
              Primero activa «Aparecer en el directorio público» en
              <a routerLink="/profile">tu perfil</a>.
            </p>
          } @else {
            <label class="toggle">
              <input
                type="checkbox"
                [checked]="settings()!.acceptsPrivateClasses"
                [disabled]="saving()"
                (change)="toggleAccepts($event)" />
              Aceptar solicitudes de clases particulares en el directorio
            </label>
          }
        </ui-card>

        <div class="list">
          @if (!requests().length) {
            <p class="muted">Todavía no tienes solicitudes.</p>
          } @else {
            @for (r of requests(); track r.id) {
              <ui-card>
                <div class="row">
                  <div>
                    <h2>{{ r.studentName }}</h2>
                    <p class="muted">{{ r.category }} · {{ r.preferredSchedule }} · {{ r.createdAt | date:'short' }}</p>
                  </div>
                  <strong class="status">{{ statusLabel(r.status) }}</strong>
                </div>
                <p>{{ r.message }}</p>
                @if (r.studentEmail) {
                  <p class="contact">Contacto: {{ r.studentEmail }}</p>
                }
                @if (r.reply) {
                  <p class="muted">Tu respuesta: {{ r.reply }}</p>
                }
                @if (r.status === 'Pending') {
                  <label class="field">Respuesta
                    <textarea [(ngModel)]="replies[r.id]" rows="2"
                      placeholder="Si aceptas, indica cómo y cuándo lo contactarás."></textarea>
                  </label>
                  <div class="actions">
                    <ui-button type="button" [disabled]="saving()" (click)="respond(r, true)">Aceptar</ui-button>
                    <ui-button type="button" variant="secondary" [disabled]="saving()" (click)="respond(r, false)">Rechazar</ui-button>
                  </div>
                }
              </ui-card>
            }
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .page { width: min(820px, calc(100% - 2rem)); margin: 0 auto; padding: 1.5rem 0 3rem; display: grid; gap: 1rem; }
    .eyebrow { margin: 0; color: var(--color-primary); font-size: var(--text-xs); font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; }
    h1 { margin: 0.25rem 0 0.35rem; }
    h2 { margin: 0 0 0.45rem; font-size: var(--text-md); }
    .muted { margin: 0; color: var(--color-text-secondary); line-height: 1.45; }
    .toggle { display: flex; gap: 0.65rem; align-items: flex-start; font-weight: 600; }
    .list { display: grid; gap: 0.85rem; }
    .row { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; }
    .status { font-size: var(--text-sm); }
    .contact { margin: 0.35rem 0 0; font-weight: 700; }
    .field { display: grid; gap: 0.3rem; margin-top: 0.65rem; font-weight: 700; font-size: var(--text-sm); }
    .field textarea {
      font: inherit; font-weight: 500; border: 1px solid var(--color-border);
      border-radius: 0.55rem; padding: 0.55rem 0.65rem; background: var(--color-background); color: var(--color-text);
    }
    .actions { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-top: 0.65rem; }
    a { color: var(--color-primary); }
  `]
})
export class TeacherPrivateClassesPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly settings = signal<SettingsDto | null>(null);
  readonly requests = signal<RequestDto[]>([]);
  replies: Record<number, string> = {};

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.http.get<SettingsDto>(`${env.apiUrl}/api/teacher/private-classes/settings`).subscribe({
      next: (s) => this.settings.set(s),
      error: (err) => this.error.set(mapApiError(err))
    });
    this.http.get<RequestDto[]>(`${env.apiUrl}/api/teacher/private-classes`).subscribe({
      next: (list) => {
        this.requests.set(list);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  toggleAccepts(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.saving.set(true);
    this.error.set(null);
    this.http.put<SettingsDto>(`${env.apiUrl}/api/teacher/private-classes/settings`, {
      acceptsPrivateClasses: input.checked
    }).subscribe({
      next: (s) => {
        this.saving.set(false);
        this.settings.set(s);
        this.ok.set(s.acceptsPrivateClasses
          ? 'Ahora los estudiantes pueden pedirte clases particulares.'
          : 'Ya no recibes solicitudes nuevas.');
      },
      error: (err) => {
        this.saving.set(false);
        input.checked = !input.checked;
        this.error.set(mapApiError(err));
      }
    });
  }

  respond(r: RequestDto, accept: boolean): void {
    this.saving.set(true);
    this.error.set(null);
    this.http.post(`${env.apiUrl}/api/teacher/private-classes/${r.id}/respond`, {
      accept,
      reply: this.replies[r.id] || null
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.ok.set(accept ? 'Solicitud aceptada. El estudiante verá tu correo.' : 'Solicitud rechazada.');
        this.reload();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Pending': return 'Pendiente';
      case 'Accepted': return 'Aceptada';
      case 'Declined': return 'Rechazada';
      case 'Cancelled': return 'Cancelada';
      default: return status;
    }
  }
}
