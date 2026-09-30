import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { PushService } from '../../../core/notifications/push.service';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';

type PushState = 'loading' | 'on' | 'off' | 'blocked' | 'ios' | 'unsupported';

/** Mi perfil card to turn this device's push notifications on/off and send a test. */
@Component({
  selector: 'app-push-settings',
  standalone: true,
  imports: [UiBadgeComponent, UiButtonComponent, UiCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-card>
      <h2>Notificaciones</h2>
      <p class="muted">Avisos en este dispositivo, con sonido y el numerito en el ícono de la app.</p>
      <p class="status">
        Estado:
        <ui-badge [tone]="tone()">{{ label() }}</ui-badge>
      </p>

      @switch (state()) {
        @case ('off') {
          <div class="row">
            <ui-button [loading]="push.busy()" (click)="enable()">Activar notificaciones</ui-button>
          </div>
        }
        @case ('on') {
          <div class="row">
            <ui-button variant="secondary" [loading]="testing()" (click)="test()">Enviar prueba</ui-button>
            <ui-button variant="ghost" [loading]="push.busy()" (click)="disable()">Desactivar</ui-button>
          </div>
          @if (testSent()) {
            <p class="muted small">Listo, en unos segundos te llega la notificación de prueba.</p>
          }
        }
        @case ('blocked') {
          <p class="muted small">
            Las bloqueaste en este navegador. Para volver a recibirlas, permite las notificaciones de este
            sitio en la configuración del navegador o del teléfono y vuelve a esta pantalla.
          </p>
        }
        @case ('ios') {
          <p class="muted small">
            En iPhone primero toca <b>Compartir</b> → <b>“Agregar a pantalla de inicio”</b>, abre Mi CALE
            desde ese ícono y vuelve aquí para activarlas.
          </p>
        }
        @case ('unsupported') {
          <p class="muted small">Este navegador no admite notificaciones. Prueba con Chrome, Edge o Safari actualizados.</p>
        }
      }
    </ui-card>
  `,
  styles: [`
    h2 { margin: 0 0 0.35rem; }
    .muted { margin: 0; color: var(--color-text-secondary); }
    .small { font-size: 0.88rem; margin-top: 0.6rem; line-height: 1.5; }
    .status { display: flex; align-items: center; gap: 0.5rem; margin: 0.8rem 0 0; font-weight: 700; }
    .row { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-top: 0.9rem; }
  `]
})
export class PushSettingsComponent implements OnInit {
  readonly push = inject(PushService);
  readonly state = signal<PushState>('loading');
  readonly testing = signal(false);
  readonly testSent = signal(false);

  readonly label = computed(() => {
    switch (this.state()) {
      case 'on': return 'Activadas';
      case 'off': return 'Desactivadas';
      case 'blocked': return 'Bloqueadas';
      case 'ios': return 'Requiere instalar la app';
      case 'unsupported': return 'No disponibles';
      default: return 'Revisando…';
    }
  });

  readonly tone = computed(() => {
    switch (this.state()) {
      case 'on': return 'success' as const;
      case 'blocked': return 'danger' as const;
      case 'off':
      case 'ios': return 'warning' as const;
      default: return 'neutral' as const;
    }
  });

  ngOnInit(): void {
    void this.refresh();
  }

  async enable(): Promise<void> {
    this.push.markPromptDone();
    await this.push.enable();
    await this.refresh();
  }

  async disable(): Promise<void> {
    await this.push.disable();
    this.testSent.set(false);
    await this.refresh();
  }

  async test(): Promise<void> {
    this.testing.set(true);
    try {
      await this.push.sendTest();
      this.testSent.set(true);
    } finally {
      this.testing.set(false);
    }
  }

  private async refresh(): Promise<void> {
    if (this.push.needsInstallOnIos) {
      this.state.set('ios');
      return;
    }
    if (!this.push.supported) {
      this.state.set('unsupported');
      return;
    }
    const permission = typeof Notification === 'undefined' ? 'default' : Notification.permission;
    this.push.permission.set(permission);
    if (permission === 'denied') {
      this.state.set('blocked');
      return;
    }
    this.state.set(await this.push.isSubscribedHere() ? 'on' : 'off');
  }
}
