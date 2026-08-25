import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { roleLabel } from '../../../shared/utils/role-label';

@Component({
  selector: 'app-admin-settings-page',
  standalone: true,
  imports: [
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiPageHeaderComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Administración"
      title="Configuración"
      subtitle="Ajustes de cuenta disponibles en el cliente." />
    <ui-card>
      <h2>Sesión actual</h2>
      <p>{{ session.user()?.name }} · {{ session.user()?.email }}</p>
      <p><ui-badge tone="primary">{{ roleLabel(session.user()?.role) }}</ui-badge></p>
      <p class="muted">No hay un API de settings de plataforma. La contraseña se cambia en Perfil.</p>
      <a routerLink="/profile"><ui-button type="button">Abrir perfil</ui-button></a>
    </ui-card>
  `
})
export class AdminSettingsPage {
  readonly session = inject(SessionStore);
  readonly roleLabel = roleLabel;
}
