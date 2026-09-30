import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { SessionStore } from '../core/auth/session.store';
import { PushService } from '../core/notifications/push.service';
import { UiButtonComponent } from '../shared/ui/ui-button.component';
import { UiIconComponent } from '../shared/ui/ui-icon.component';

type PromptState = 'hidden' | 'ask' | 'ios' | 'denied' | 'done';

/** Invites every role to turn on phone/desktop notifications for this device. */
@Component({
  selector: 'app-push-prompt',
  standalone: true,
  imports: [UiButtonComponent, UiIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (state()) {
      @case ('ask') {
        <section class="push-card" role="region" aria-label="Activar notificaciones">
          <span class="push-ico" aria-hidden="true"><ui-icon name="bell" size="md" /></span>
          <div class="push-text">
            <strong>Activa las notificaciones</strong>
            <span>{{ askText }}</span>
          </div>
          <div class="push-actions">
            <ui-button [loading]="push.busy()" (click)="activate()">Activar notificaciones</ui-button>
            <button type="button" class="push-later" (click)="later()">Ahora no</button>
          </div>
        </section>
      }
      @case ('ios') {
        <section class="push-card" role="region" aria-label="Notificaciones en iPhone">
          <span class="push-ico" aria-hidden="true"><ui-icon name="bell" size="md" /></span>
          <div class="push-text">
            <strong>¿Quieres recibir avisos en tu iPhone?</strong>
            <span>Toca <b>Compartir</b> y luego <b>“Agregar a pantalla de inicio”</b>. Abre Mi CALE desde ese ícono y aquí podrás activar las notificaciones.</span>
          </div>
          <div class="push-actions">
            <button type="button" class="push-later" (click)="later()">Entendido</button>
          </div>
        </section>
      }
      @case ('denied') {
        <section class="push-card warn" role="region" aria-label="Notificaciones bloqueadas">
          <span class="push-ico" aria-hidden="true"><ui-icon name="bell" size="md" /></span>
          <div class="push-text">
            <strong>Las notificaciones están bloqueadas</strong>
            <span>Para recibir avisos, permite las notificaciones de este sitio en la configuración del navegador o del teléfono.</span>
          </div>
          <div class="push-actions">
            <button type="button" class="push-later" (click)="later()">Entendido</button>
          </div>
        </section>
      }
      @case ('done') {
        <section class="push-card ok" role="status">
          <span class="push-ico" aria-hidden="true"><ui-icon name="bell" size="md" /></span>
          <div class="push-text">
            <strong>¡Listo! Notificaciones activadas</strong>
            <span>Te llegará una notificación de prueba en unos segundos.</span>
          </div>
          <div class="push-actions">
            <button type="button" class="push-later" (click)="state.set('hidden')">Cerrar</button>
          </div>
        </section>
      }
    }
  `,
  styles: [`
    .push-card {
      display: flex;
      align-items: center;
      gap: 0.9rem;
      margin: 0 0 1rem;
      padding: 0.85rem 1rem;
      border-radius: var(--radius-md);
      background: color-mix(in srgb, var(--color-primary) 10%, var(--color-surface));
      border: 1px solid color-mix(in srgb, var(--color-primary) 30%, var(--color-border));
    }
    .push-card.warn {
      background: color-mix(in srgb, #f59e0b 12%, var(--color-surface));
      border-color: color-mix(in srgb, #f59e0b 40%, var(--color-border));
    }
    .push-card.ok {
      background: color-mix(in srgb, #22c55e 12%, var(--color-surface));
      border-color: color-mix(in srgb, #22c55e 40%, var(--color-border));
    }
    .push-ico {
      display: grid;
      place-items: center;
      flex: 0 0 auto;
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 50%;
      background: var(--color-primary);
      color: #fff;
    }
    .push-text {
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
      flex: 1 1 auto;
      min-width: 0;
      font-size: 0.92rem;
      color: var(--color-text-muted, inherit);
    }
    .push-text strong {
      color: var(--color-text, inherit);
      font-size: 1rem;
    }
    .push-actions {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex: 0 0 auto;
    }
    .push-actions ::ng-deep .btn {
      white-space: nowrap;
    }
    .push-later {
      white-space: nowrap;
      background: none;
      border: 0;
      padding: 0.45rem 0.6rem;
      color: var(--color-text-muted, inherit);
      font-weight: 600;
      cursor: pointer;
      border-radius: var(--radius-sm, 6px);
    }
    .push-later:hover {
      background: color-mix(in srgb, var(--color-text, #000) 8%, transparent);
    }
    @media (max-width: 600px) {
      .push-card {
        flex-wrap: wrap;
        align-items: flex-start;
      }
      .push-text {
        flex-basis: calc(100% - 3.5rem);
      }
      .push-actions {
        width: 100%;
        justify-content: space-between;
      }
    }
  `]
})
export class PushPromptComponent {
  readonly push = inject(PushService);
  private readonly session = inject(SessionStore);
  readonly state = signal<PromptState>(this.initialState());

  get askText(): string {
    return this.session.user()?.role === 'Student'
      ? 'Te avisaremos al instante, con sonido y el numerito en el ícono, cuando tu docente o tu escuela te escriban.'
      : 'Te avisaremos al instante, con sonido y el numerito en el ícono, cuando tengas mensajes, entregas o novedades.';
  }

  async activate(): Promise<void> {
    const ok = await this.push.enable();
    if (ok) {
      this.state.set('done');
    } else if (this.push.permission() === 'denied') {
      this.state.set('denied');
    }
  }

  later(): void {
    this.push.dismiss();
    this.state.set('hidden');
  }

  private initialState(): PromptState {
    if (this.push.dismissedRecently) return 'hidden';
    if (this.push.needsInstallOnIos) return 'ios';
    if (!this.push.supported) return 'hidden';
    const permission = this.push.permission();
    if (permission === 'default') return 'ask';
    if (permission === 'denied') return 'denied';
    return 'hidden';
  }
}
