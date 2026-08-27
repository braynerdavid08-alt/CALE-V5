import { Component, Input, OnChanges, OnInit, SimpleChanges, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiButtonComponent } from './ui-button.component';

export type OnboardingRole = 'Teacher' | 'Student' | 'School';

export interface OnboardingTip {
  title: string;
  text: string;
  link?: string;
  linkLabel?: string;
}

const TIPS: Record<OnboardingRole, OnboardingTip[]> = {
  Teacher: [
    {
      title: 'Aula en Vivo',
      text: 'Crea una sala, proyecta el QR y conduce el simulacro en tiempo real.',
      link: '/teacher/live',
      linkLabel: 'Abrir Live'
    },
    {
      title: 'Grupos',
      text: 'Organiza estudiantes, publica actividades y sigue el rendimiento.',
      link: '/teacher/groups',
      linkLabel: 'Mis grupos'
    },
    {
      title: 'Biblioteca',
      text: 'Reutiliza bancos y preguntas para armar evaluaciones más rápido.',
      link: '/teacher/library',
      linkLabel: 'Ver biblioteca'
    }
  ],
  Student: [
    {
      title: 'Unirse a un grupo',
      text: 'Pide el código a tu instructor e ingresa desde el panel o Mis clases.',
      link: '/student/classes',
      linkLabel: 'Mis clases'
    },
    {
      title: 'Simulacro',
      text: 'Practica con el simulador y revisa tu progreso por temas.',
      link: '/student/simulator',
      linkLabel: 'Ir al simulador'
    },
    {
      title: 'Unirse en vivo',
      text: 'Con el código de sala puedes entrar a la actividad Live del instructor.',
      link: '/live/join',
      linkLabel: 'Entrar a Live'
    }
  ],
  School: [
    {
      title: 'Membresía',
      text: 'Solicita el plan, paga y sube el comprobante para activar cupos.',
      link: '/school/membership',
      linkLabel: 'Membresía'
    },
    {
      title: 'Usuarios',
      text: 'Crea instructores y estudiantes, y administra sus accesos.',
      link: '/school/users',
      linkLabel: 'Usuarios'
    },
    {
      title: 'Importar CSV',
      text: 'Carga listas de usuarios en bloque desde un archivo CSV.',
      link: '/school/import',
      linkLabel: 'Importar'
    }
  ]
};

@Component({
  selector: 'ui-onboarding',
  standalone: true,
  imports: [RouterLink, UiButtonComponent],
  template: `
    @if (visible()) {
      <aside class="ob" role="region" [attr.aria-label]="'Guía rápida ' + role">
        <div class="ob-head">
          <div>
            <p class="ob-kicker">Primeros pasos</p>
            <h2>Guía rápida</h2>
          </div>
          <ui-button type="button" variant="ghost" (click)="dismiss()">Entendido</ui-button>
        </div>
        <ul class="ob-tips">
          @for (tip of tips(); track tip.title) {
            <li>
              <strong>{{ tip.title }}</strong>
              <p>{{ tip.text }}</p>
              @if (tip.link) {
                <a [routerLink]="tip.link">{{ tip.linkLabel || 'Abrir' }}</a>
              }
            </li>
          }
        </ul>
      </aside>
    }
  `,
  styles: [`
    :host { display: block; }

    .ob {
      margin: 0 0 1.25rem;
      padding: 1rem 1.1rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      background:
        linear-gradient(
          145deg,
          color-mix(in srgb, var(--color-primary) 12%, var(--color-surface)),
          var(--color-surface) 60%
        );
    }

    .ob-head {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 0.75rem;
      margin-bottom: 0.85rem;
    }

    .ob-kicker {
      margin: 0;
      color: var(--color-primary);
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.06em;
      text-transform: uppercase;
    }

    h2 {
      margin: 0.15rem 0 0;
      font-size: var(--text-lg);
      font-weight: 800;
    }

    .ob-tips {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.75rem;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
    }

    .ob-tips li {
      padding: 0.75rem 0.85rem;
      border-radius: var(--radius-md);
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      display: grid;
      gap: 0.35rem;
    }

    .ob-tips strong {
      font-size: var(--text-sm);
      font-weight: 800;
    }

    .ob-tips p {
      margin: 0;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      line-height: 1.4;
    }

    .ob-tips a {
      color: var(--color-primary);
      font-weight: 700;
      font-size: var(--text-sm);
      text-decoration: none;
    }

    .ob-tips a:hover {
      text-decoration: underline;
    }
  `]
})
export class UiOnboardingComponent implements OnInit, OnChanges {
  @Input({ required: true }) role!: OnboardingRole;
  @Input() tipsOverride: OnboardingTip[] | null = null;

  readonly visible = signal(false);
  readonly tips = signal<OnboardingTip[]>([]);

  ngOnInit(): void {
    this.refresh();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['role'] || changes['tipsOverride']) {
      this.refresh();
    }
  }

  dismiss(): void {
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.setItem(this.storageKey(), '1');
    }
    this.visible.set(false);
  }

  private refresh(): void {
    const role = this.role;
    if (!role) {
      this.visible.set(false);
      this.tips.set([]);
      return;
    }
    this.tips.set(this.tipsOverride?.length ? this.tipsOverride : TIPS[role] ?? []);
    const dismissed =
      typeof sessionStorage !== 'undefined' && sessionStorage.getItem(this.storageKey()) === '1';
    this.visible.set(!dismissed && this.tips().length > 0);
  }

  private storageKey(): string {
    return `cale.onboarding.v1.${this.role}`;
  }
}
