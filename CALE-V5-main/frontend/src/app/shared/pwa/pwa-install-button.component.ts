import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  HostListener,
  Input,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { BRAND } from '../../core/brand';
import { PwaInstallMode, PwaInstallService } from '../../core/pwa/pwa-install.service';
import { UiButtonComponent } from '../ui/ui-button.component';
import { UiIconComponent } from '../ui/ui-icon.component';

/**
 * “Instalar Luz Verde” CTA + install / iOS guide modal.
 * Safe for all roles; does not touch auth or routing.
 */
@Component({
  selector: 'app-pwa-install-button',
  standalone: true,
  imports: [UiButtonComponent, UiIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (show()) {
      @if (mode() === 'installed') {
        @if (variant !== 'compact') {
          <p class="installed" role="status">
            <ui-icon name="home" />
            {{ brand.name }} ya está en tu pantalla de inicio.
          </p>
        }
      } @else {
        @if (variant === 'compact') {
          <ui-button
            type="button"
            variant="ghost"
            class="install-compact"
            [attr.aria-label]="'Instalar ' + brand.name"
            title="Instalar en tu celular"
            (click)="onInstallClick()">
            <ui-icon name="download" />
            <span class="compact-label">Instalar</span>
          </ui-button>
        } @else if (variant === 'card') {
          <section class="card" aria-labelledby="pwa-install-title">
            <div class="card-head">
              <span class="card-ico" aria-hidden="true"><ui-icon name="download" size="lg" /></span>
              <div>
                <h2 id="pwa-install-title">Instalar {{ brand.name }}</h2>
                <p class="muted">
                  Agrégala a la pantalla de inicio de tu celular y ábrela como una app,
                  sin ocupar la tienda.
                </p>
              </div>
            </div>
            <div class="card-actions">
              <ui-button type="button" [loading]="busy()" (click)="onInstallClick()">
                <ui-icon name="download" />
                Instalar {{ brand.name }}
              </ui-button>
              @if (mode() === 'ios-guide' || mode() === 'manual') {
                <ui-button type="button" variant="secondary" (click)="openGuide()">
                  Ver instrucciones
                </ui-button>
              }
            </div>
          </section>
        } @else {
          <ui-button
            type="button"
            [variant]="buttonVariant"
            [loading]="busy()"
            (click)="onInstallClick()">
            <ui-icon name="download" />
            Instalar {{ brand.name }}
          </ui-button>
        }
      }
    }

    @if (guideOpen()) {
      <div class="backdrop" (click)="closeGuide()"></div>
      <section
        class="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="pwa-guide-title"
        tabindex="-1">
        <button type="button" class="x" aria-label="Cerrar" (click)="closeGuide()">✕</button>
        <span class="dialog-ico" aria-hidden="true">
          <ui-icon [name]="guideKind() === 'ios' ? 'share' : 'download'" size="lg" />
        </span>
        <h2 id="pwa-guide-title">{{ guideTitle() }}</h2>
        @if (guideKind() === 'ios') {
          <p class="lead">
            En iPhone y iPad debes usar <strong>Safari</strong> (no Chrome ni otro navegador).
            La instalación es manual:
          </p>
          <ol class="steps">
            <li>
              Abre esta página en <strong>Safari</strong>.
            </li>
            <li>
              Toca <strong>Compartir</strong>
              <span class="inline-ico" aria-hidden="true"><ui-icon name="share" /></span>
              (cuadrado con flecha hacia arriba, abajo en la barra).
            </li>
            <li>
              En la lista, <strong>desplázate hacia abajo</strong> hasta
              <strong>Añadir a pantalla de inicio</strong>
              (también puede decir “Agregar a inicio”).
            </li>
            <li>
              Si aparece <strong>Abrir como app web</strong>, déjala <strong>activada</strong>.
            </li>
            <li>Pulsa <strong>Añadir</strong>.</li>
          </ol>
          <p class="hint warn">
            No uses “Solicitar versión de escritorio”: solo recarga la página y no instala nada.
          </p>
          <p class="hint">
            Luego abre {{ brand.name }} desde el ícono nuevo en tu pantalla de inicio.
            Si no ves “Añadir a pantalla de inicio”, toca <strong>Editar acciones…</strong>
            al final de la hoja de compartir y actívala.
          </p>
        } @else {
          <p class="lead">
            Tu navegador no mostró el diálogo automático. Puedes instalarla así:
          </p>
          <ol class="steps">
            <li>Abre el menú del navegador (⋮ o ⋯).</li>
            <li>Busca <strong>Instalar aplicación</strong> o <strong>Añadir a la pantalla de inicio</strong>.</li>
            <li>Confirma la instalación.</li>
          </ol>
          <p class="hint">
            En Android con Chrome suele aparecer un aviso “Instalar app”. Si no lo ves,
            usa el menú del navegador.
          </p>
        }
        <div class="dialog-actions">
          <ui-button type="button" (click)="closeGuide()">Entendido</ui-button>
        </div>
      </section>
    }
  `,
  styles: [`
    :host { display: block; }
    .installed {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      line-height: 1.45;
    }
    .card {
      background: var(--color-surface);
      color: var(--color-text);
      border: 1px solid var(--color-border-strong, var(--color-border));
      border-radius: var(--radius-lg);
      padding: 1.15rem 1.25rem;
    }
    .card-head {
      display: flex;
      gap: 0.85rem;
      align-items: flex-start;
    }
    .card-ico {
      flex: none;
      display: grid;
      place-items: center;
      width: 3rem;
      height: 3rem;
      border-radius: 50%;
      background: color-mix(in srgb, var(--color-primary) 18%, var(--color-surface));
      color: var(--color-primary);
    }
    .card h2 {
      margin: 0 0 0.35rem;
      font-size: 1.1rem;
      color: var(--color-text);
    }
    .muted {
      margin: 0;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      line-height: 1.45;
    }
    .card-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.55rem;
      margin-top: 0.95rem;
    }
    .install-compact { white-space: nowrap; }
    .compact-label { margin-left: 0.15rem; }
    @media (max-width: 720px) {
      .compact-label { display: none; }
    }

    .backdrop {
      position: fixed;
      inset: 0;
      z-index: 1100;
      background: rgb(0 0 0 / 0.55);
      animation: fade 0.2s ease-out;
    }
    .dialog {
      position: fixed;
      z-index: 1101;
      left: 50%;
      top: 50%;
      transform: translate(-50%, -50%);
      width: min(94vw, 26rem);
      max-height: min(88dvh, 36rem);
      overflow: auto;
      display: grid;
      justify-items: stretch;
      gap: 0.55rem;
      padding: 1.35rem 1.25rem 1.15rem;
      text-align: left;
      border-radius: 18px;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      box-shadow: 0 24px 60px rgb(0 0 0 / 0.45);
      animation: pop 0.22s ease-out;
      outline: none;
    }
    .x {
      position: absolute;
      top: 0.55rem;
      right: 0.55rem;
      width: 2.6rem;
      height: 2.6rem;
      border: 1px solid var(--color-border);
      border-radius: 999px;
      background: transparent;
      color: var(--color-text);
      font-size: 1.05rem;
      cursor: pointer;
    }
    .dialog-ico {
      display: grid;
      place-items: center;
      width: 3.2rem;
      height: 3.2rem;
      margin: 0.15rem auto 0;
      border-radius: 50%;
      background: var(--color-primary);
      color: #fff;
    }
    .dialog h2 {
      margin: 0.15rem 0 0;
      text-align: center;
      font-size: 1.2rem;
      padding-right: 1.5rem;
    }
    .lead {
      margin: 0;
      color: var(--color-text-secondary);
      line-height: 1.5;
      font-size: 0.95rem;
    }
    .steps {
      margin: 0.25rem 0 0;
      padding-left: 1.25rem;
      color: var(--color-text);
      line-height: 1.55;
      font-size: 0.95rem;
    }
    .steps li + li { margin-top: 0.45rem; }
    .inline-ico {
      display: inline-flex;
      vertical-align: middle;
      margin: 0 0.15rem;
      color: var(--color-primary);
    }
    .hint {
      margin: 0.15rem 0 0;
      color: var(--color-text-secondary);
      font-size: 0.85rem;
      line-height: 1.45;
    }
    .hint.warn {
      color: var(--color-text);
      background: color-mix(in srgb, var(--color-warning, #e6a700) 18%, var(--color-surface));
      border: 1px solid color-mix(in srgb, var(--color-warning, #e6a700) 40%, var(--color-border));
      border-radius: 10px;
      padding: 0.55rem 0.7rem;
    }
    .dialog-actions {
      display: flex;
      justify-content: center;
      margin-top: 0.5rem;
    }
    @keyframes fade { from { opacity: 0; } }
    @keyframes pop {
      from { opacity: 0; transform: translate(-50%, -46%) scale(0.96); }
    }
    @media (prefers-reduced-motion: reduce) {
      .backdrop, .dialog { animation: none; }
    }
  `]
})
export class PwaInstallButtonComponent implements OnInit {
  readonly brand = BRAND;
  private readonly pwa = inject(PwaInstallService);
  private readonly destroyRef = inject(DestroyRef);

  /** default = plain button; compact = topbar; card = profile panel */
  @Input() variant: 'default' | 'compact' | 'card' = 'default';
  @Input() buttonVariant: 'primary' | 'secondary' | 'ghost' = 'primary';

  readonly mode = signal<PwaInstallMode>('hidden');
  readonly guideOpen = signal(false);
  readonly guideKind = signal<'ios' | 'manual'>('ios');
  readonly busy = signal(false);

  readonly show = computed(() => {
    const m = this.mode();
    if (this.variant === 'compact') {
      return m === 'native' || m === 'ios-guide' || m === 'manual';
    }
    return m !== 'hidden';
  });

  ngOnInit(): void {
    this.syncMode();
    if (typeof window === 'undefined') {
      return;
    }
    const onChange = () => this.syncMode();
    window.addEventListener('beforeinstallprompt', onChange);
    window.addEventListener('appinstalled', onChange);
    document.addEventListener('visibilitychange', onChange);
    this.destroyRef.onDestroy(() => {
      window.removeEventListener('beforeinstallprompt', onChange);
      window.removeEventListener('appinstalled', onChange);
      document.removeEventListener('visibilitychange', onChange);
    });
  }

  guideTitle(): string {
    return this.guideKind() === 'ios'
      ? `Agregar ${this.brand.name} al inicio`
      : `Cómo instalar ${this.brand.name}`;
  }

  async onInstallClick(): Promise<void> {
    this.syncMode();
    const m = this.mode();
    if (m === 'native') {
      this.busy.set(true);
      const result = await this.pwa.promptNativeInstall();
      this.busy.set(false);
      this.syncMode();
      if (result === 'unavailable') {
        this.openGuidePanel('manual');
      }
      return;
    }
    this.openGuidePanel(m === 'ios-guide' ? 'ios' : 'manual');
  }

  openGuide(): void {
    this.syncMode();
    this.openGuidePanel(this.mode() === 'ios-guide' ? 'ios' : 'manual');
  }

  closeGuide(): void {
    this.guideOpen.set(false);
    this.syncMode();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.guideOpen()) {
      this.closeGuide();
    }
  }

  private openGuidePanel(kind: 'ios' | 'manual'): void {
    this.guideKind.set(kind);
    this.guideOpen.set(true);
    queueMicrotask(() => {
      const dialog = document.querySelector<HTMLElement>('.dialog[aria-labelledby="pwa-guide-title"]');
      dialog?.focus();
    });
  }

  private syncMode(): void {
    this.mode.set(this.pwa.mode());
  }
}
