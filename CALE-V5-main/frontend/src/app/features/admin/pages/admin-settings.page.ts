import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { ThemeService } from '../../../core/theme/theme.service';
import { env } from '../../../core/config/env';
import { AuthFacade } from '../../auth/application/auth.facade';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { roleLabel } from '../../../shared/utils/role-label';

@Component({
  selector: 'app-admin-settings-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiErrorComponent,
    UiPageHeaderComponent,
    UiSuccessComponent
  ],
  styles: [`
    .muted { color: var(--color-text-secondary); margin: 0.35rem 0 0; }
    .facts {
      margin: 0;
      display: grid;
      gap: 0.75rem;
    }
    .facts div { display: grid; gap: 0.15rem; }
    dt {
      font-size: var(--text-xs);
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--color-text-secondary);
    }
    dd { margin: 0; font-weight: 600; }
    .stack { display: grid; gap: 0.75rem; }
    .field { display: grid; gap: 0.35rem; font-weight: 600; font-size: var(--text-sm); }
    .field input {
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: 0.55rem 0.7rem;
      background: var(--color-surface);
      color: var(--color-text);
    }
    .theme-row {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      margin-top: 0.75rem;
    }
    .theme-row button.active {
      outline: 2px solid var(--color-primary);
      outline-offset: 1px;
    }
    .list {
      margin: 0;
      padding-left: 1.1rem;
      display: grid;
      gap: 0.4rem;
      color: var(--color-text-secondary);
    }
  `],
  template: `
    <ui-page-header
      eyebrow="Administración"
      title="Configuración"
      subtitle="Apariencia, cuenta y parámetros fijos de la plataforma." />

    <ui-error [message]="auth.error() || localError()" />
    <ui-success [message]="auth.success()" />

    <div class="grid-2">
      <ui-card>
        <h2>Apariencia</h2>
        <p class="muted">Tema de la interfaz para esta sesión en el navegador.</p>
        <dl class="facts" style="margin-top: 0.75rem;">
          <div>
            <dt>Tema actual</dt>
            <dd>{{ theme.mode() === 'dark' ? 'Noche' : 'Día' }}</dd>
          </div>
        </dl>
        <div class="theme-row">
          <ui-button
            type="button"
            [variant]="theme.mode() === 'light' ? 'primary' : 'secondary'"
            (click)="setTheme('light')">
            Día
          </ui-button>
          <ui-button
            type="button"
            [variant]="theme.mode() === 'dark' ? 'primary' : 'secondary'"
            (click)="setTheme('dark')">
            Noche
          </ui-button>
        </div>
      </ui-card>

      <ui-card>
        <h2>Cuenta</h2>
        <dl class="facts">
          <div>
            <dt>Nombre</dt>
            <dd>{{ session.user()?.name }}</dd>
          </div>
          <div>
            <dt>Correo</dt>
            <dd>{{ session.user()?.email }}</dd>
          </div>
          <div>
            <dt>Rol</dt>
            <dd><ui-badge tone="primary">{{ roleLabel(session.user()?.role) }}</ui-badge></dd>
          </div>
        </dl>
        <div class="row" style="margin-top: 0.85rem;">
          <a routerLink="/profile"><ui-button type="button" variant="secondary">Ver perfil</ui-button></a>
        </div>
      </ui-card>
    </div>

    <div class="grid-2" style="margin-top: 1rem;">
      <ui-card>
        <h2>Cambiar contraseña</h2>
        <p class="muted">Actualiza la contraseña de la cuenta administradora.</p>
        <form class="stack" style="margin-top: 0.75rem;" [formGroup]="passwordForm" (ngSubmit)="changePassword()">
          <label class="field">
            Contraseña actual
            <input type="password" formControlName="currentPassword" autocomplete="current-password" />
          </label>
          <label class="field">
            Nueva contraseña
            <input type="password" formControlName="newPassword" autocomplete="new-password" />
          </label>
          <ui-button type="submit" [loading]="auth.loading()" [disabled]="passwordForm.invalid">
            Guardar contraseña
          </ui-button>
        </form>
      </ui-card>

      <ui-card>
        <h2>Conexión</h2>
        <dl class="facts">
          <div>
            <dt>API</dt>
            <dd>{{ apiUrl }}</dd>
          </div>
          <div>
            <dt>Entorno UI</dt>
            <dd>Angular · localhost:4200</dd>
          </div>
        </dl>
        <p class="muted">
          Si la API no responde, revisa que el backend esté en marcha antes de usar el resto del panel.
        </p>
      </ui-card>
    </div>

    <ui-card style="margin-top: 1rem;">
      <h2>Parámetros de plataforma</h2>
      <p class="muted">Reglas operativas vigentes (no editables desde esta pantalla en el MVP).</p>
      <ul class="list" style="margin-top: 0.75rem;">
        <li>Aprobación de exámenes y simulador: ≥ 80%.</li>
        <li>El tiempo de examen lo controla el servidor (inicio, vencimiento y cierre).</li>
        <li>Registro público: estudiante, docente y escuela (rutas separadas).</li>
        <li>Solo Admin crea o edita preguntas y bancos; Escuela y Docente los heredan en lectura.</li>
        <li>Escuelas gestionan cupos, membresía y vinculan docentes/estudiantes existentes.</li>
        <li>Bancos oficiales sembrados: Normas de tránsito (500) y Reconocimiento visual de señales (194).</li>
      </ul>
    </ui-card>
  `
})
export class AdminSettingsPage {
  private readonly fb = inject(FormBuilder);
  readonly session = inject(SessionStore);
  readonly theme = inject(ThemeService);
  readonly auth = inject(AuthFacade);
  readonly roleLabel = roleLabel;
  readonly apiUrl = env.apiUrl;
  readonly localError = signal<string | null>(null);

  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8)]]
  });

  setTheme(mode: 'light' | 'dark'): void {
    this.theme.set(mode);
  }

  changePassword(): void {
    this.localError.set(null);
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      this.localError.set('Completa la contraseña actual y una nueva de al menos 8 caracteres.');
      return;
    }
    const { currentPassword, newPassword } = this.passwordForm.getRawValue();
    this.auth.changePassword(currentPassword, newPassword);
    this.passwordForm.reset();
  }
}
