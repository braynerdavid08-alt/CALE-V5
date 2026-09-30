import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  HostListener,
  OnInit,
  inject,
  signal
} from '@angular/core';
import { SessionStore } from '../core/auth/session.store';
import { PushService } from '../core/notifications/push.service';
import { UiButtonComponent } from '../shared/ui/ui-button.component';
import { UiIconComponent } from '../shared/ui/ui-icon.component';

type PromptKind = 'ask' | 'ios';

/**
 * One-time popup inviting the user to turn on notifications on this device.
 * Closes on any answer; afterwards the setting lives in Mi perfil → Preferencias.
 */
@Component({
  selector: 'app-push-prompt',
  standalone: true,
  imports: [UiButtonComponent, UiIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (kind(); as k) {
      <div class="backdrop" (click)="later()"></div>
      <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="push-title">
        <span class="ico" aria-hidden="true"><ui-icon name="bell" size="lg" /></span>
        @if (k === 'ask') {
          <h2 id="push-title">¿Activamos las notificaciones?</h2>
          <p>{{ askText }}</p>
          <div class="actions">
            <ui-button [loading]="push.busy()" (click)="activate()">Sí, activar</ui-button>
            <button type="button" class="later" (click)="later()">Ahora no</button>
          </div>
        } @else {
          <h2 id="push-title">Recibe avisos en tu iPhone</h2>
          <p>
            Toca <b>Compartir</b> y luego <b>“Agregar a pantalla de inicio”</b>.
            Abre Mi CALE desde ese ícono y activa las notificaciones en <b>Mi perfil</b>.
          </p>
          <div class="actions">
            <ui-button (click)="later()">Entendido</ui-button>
          </div>
        }
        <p class="hint">Puedes cambiarlo cuando quieras en Mi perfil → Preferencias.</p>
      </section>
    }
  `,
  styles: [`
    .backdrop {
      position: fixed;
      inset: 0;
      z-index: 1000;
      background: rgb(0 0 0 / 0.55);
      animation: fade 0.2s ease-out;
    }
    .dialog {
      position: fixed;
      z-index: 1001;
      left: 50%;
      top: 50%;
      transform: translate(-50%, -50%);
      width: min(92vw, 26rem);
      display: grid;
      justify-items: center;
      gap: 0.65rem;
      padding: 1.5rem 1.4rem 1.2rem;
      text-align: center;
      border-radius: 18px;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      box-shadow: 0 24px 60px rgb(0 0 0 / 0.45);
      animation: pop 0.22s ease-out;
    }
    .ico {
      display: grid;
      place-items: center;
      width: 3.4rem;
      height: 3.4rem;
      border-radius: 50%;
      background: var(--color-primary);
      color: #fff;
    }
    h2 { margin: 0.2rem 0 0; font-size: 1.25rem; }
    p { margin: 0; color: var(--color-text-secondary); line-height: 1.5; }
    .actions {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      align-items: center;
      gap: 0.6rem;
      margin-top: 0.4rem;
    }
    .later {
      background: none;
      border: 0;
      padding: 0.55rem 0.8rem;
      color: var(--color-text-secondary);
      font: inherit;
      font-weight: 700;
      cursor: pointer;
      border-radius: 8px;
    }
    .later:hover { background: color-mix(in srgb, var(--color-text) 8%, transparent); }
    .hint { font-size: 0.8rem; opacity: 0.8; }
    @keyframes fade { from { opacity: 0; } }
    @keyframes pop { from { opacity: 0; transform: translate(-50%, -46%) scale(0.96); } }
    @media (prefers-reduced-motion: reduce) {
      .backdrop, .dialog { animation: none; }
    }
  `]
})
export class PushPromptComponent implements OnInit {
  readonly push = inject(PushService);
  private readonly session = inject(SessionStore);
  private readonly destroyRef = inject(DestroyRef);
  readonly kind = signal<PromptKind | null>(null);

  get askText(): string {
    return this.session.user()?.role === 'Student'
      ? 'Te avisamos al instante, con sonido y el numerito en el ícono, cuando tu docente o tu escuela te escriban… y cuando se te olvide practicar 😏.'
      : 'Te avisamos al instante, con sonido y el numerito en el ícono, cuando tengas mensajes, entregas o novedades.';
  }

  ngOnInit(): void {
    const kind = this.initialKind();
    if (!kind) return;
    const timer = setTimeout(() => this.kind.set(kind), 1200);
    this.destroyRef.onDestroy(() => clearTimeout(timer));
  }

  async activate(): Promise<void> {
    await this.push.enable();
    this.close();
  }

  later(): void {
    this.close();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.kind()) this.close();
  }

  private close(): void {
    this.push.markPromptDone();
    this.kind.set(null);
  }

  private initialKind(): PromptKind | null {
    if (this.push.promptDone) return null;
    if (this.push.needsInstallOnIos) return 'ios';
    if (!this.push.supported || this.push.permission() !== 'default') return null;
    return 'ask';
  }
}
