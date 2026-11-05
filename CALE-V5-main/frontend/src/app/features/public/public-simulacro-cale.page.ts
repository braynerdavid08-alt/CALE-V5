import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BRAND } from '../../core/brand';
import { PAGE_SEO } from '../../core/seo/page-seo';
import { SeoService } from '../../core/seo/seo.service';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';

const FAQS = [
  {
    question: '¿Qué es el simulacro CALE?',
    answer:
      'Es una práctica de preguntas teóricas de formación vial en Luz Verde para preparar evaluaciones de conducción. No es un examen oficial.'
  },
  {
    question: '¿Cómo puedo practicar mi examen CALE?',
    answer:
      'Crea una cuenta de estudiante en Luz Verde, inicia sesión y entra al simulador desde tu panel. Desde esta página puedes ir directo al registro o al ingreso.'
  },
  {
    question: '¿Dónde puedo practicar preguntas de conducción?',
    answer:
      'En el simulador de Luz Verde puedes resolver preguntas relacionadas con normas de tránsito, señales y seguridad vial según el contenido disponible en la plataforma.'
  },
  {
    question: '¿Qué temas puedo estudiar?',
    answer:
      'La plataforma incluye formación vial con énfasis en normas, señales, vía, vehículo y categorías de licencia según la malla publicada por Luz Verde.'
  },
  {
    question: '¿Puedo repetir el simulacro?',
    answer:
      'Sí. Puedes practicar varias veces desde tu cuenta de estudiante, de acuerdo con el acceso que tenga tu perfil y tu escuela.'
  },
  {
    question: '¿Cómo puedo practicar señales de tránsito?',
    answer:
      'Usa el simulacro y los recursos de formación de Luz Verde. También puedes explorar juegos y prácticas de señales cuando estén habilitados en tu cuenta.'
  },
  {
    question: '¿Cómo puedo estudiar normas de tránsito?',
    answer:
      'Revisa la malla y los bancos de práctica disponibles en Luz Verde, y combina estudio con simulacros para reforzar lo aprendido.'
  },
  {
    question: '¿Necesito registrarme para utilizar el simulacro?',
    answer:
      'Sí. El simulador operativo requiere iniciar sesión. Esta página pública explica el servicio y te guía al registro o al ingreso.'
  },
  {
    question: '¿El simulacro de Luz Verde es un examen oficial?',
    answer:
      'No. Es una herramienta de preparación. No reemplaza evaluaciones oficiales ni garantiza aprobar un examen de tránsito.'
  },
  {
    question: '¿Cómo puedo conocer otras herramientas de formación vial?',
    answer:
      'Desde Luz Verde puedes explorar cursos públicos, el directorio de escuelas vinculadas, instructores y el resto de la plataforma según tu rol.'
  }
] as const;

@Component({
  selector: 'app-public-simulacro-cale-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent],
  template: `
    <div class="page">
      <header class="head">
        <p class="eyebrow">Simulacro CALE</p>
        <h1>Practica tu examen CALE con el simulacro de {{ brand.name }}</h1>
        <p class="lead">
          Prepárate para tu formación como conductor con preguntas de normas de tránsito, señales viales
          y seguridad vial. El simulacro de {{ brand.name }} es una herramienta de práctica: no es un examen oficial.
        </p>
        <div class="cta">
          <ui-button routerLink="/register" type="button">Practicar mi examen CALE</ui-button>
          <ui-button routerLink="/login" type="button" variant="secondary">Iniciar simulacro</ui-button>
        </div>
      </header>

      <section class="block">
        <h2>Qué es el simulacro CALE en {{ brand.name }}</h2>
        <p>
          Es un espacio para practicar el tipo de preguntas teóricas que acompañan la formación vial.
          Sirve para medir tu preparación, identificar temas débiles y repetir intentos desde tu cuenta.
        </p>
      </section>

      <section class="block">
        <h2>Cómo funciona</h2>
        <ol>
          <li>Regístrate o inicia sesión como estudiante.</li>
          <li>Abre el simulador desde tu panel.</li>
          <li>Responde, revisa resultados y vuelve a practicar.</li>
        </ol>
        <p class="note">
          El acceso al simulador operativo está en
          <a routerLink="/student/simulator">/student/simulator</a>
          (requiere cuenta e inicio de sesión).
        </p>
      </section>

      <section class="block">
        <h2>Temas que puedes reforzar</h2>
        <ul>
          <li>Normas de tránsito</li>
          <li>Señales de tránsito</li>
          <li>Seguridad vial</li>
          <li>Comportamiento del conductor</li>
          <li>Contenidos de la malla vial publicada en la plataforma</li>
        </ul>
      </section>

      <section class="block">
        <h2>Preguntas frecuentes</h2>
        <div class="faq">
          @for (item of faqs; track item.question) {
            <details>
              <summary>{{ item.question }}</summary>
              <p>{{ item.answer }}</p>
            </details>
          }
        </div>
      </section>

      <section class="block">
        <h2>Sigue explorando {{ brand.name }}</h2>
        <div class="cta">
          <a routerLink="/estudiantes">Recursos para estudiantes</a>
          <a routerLink="/escuelas">Escuelas de conducción vinculadas</a>
          <a routerLink="/para-cea">Software para CEA</a>
          <a routerLink="/cursos">Malla y cursos</a>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .page {
      width: min(820px, calc(100% - 2rem));
      margin: 0 auto;
      padding: 2.5rem 0 3.5rem;
    }
    .eyebrow {
      margin: 0 0 0.35rem;
      color: var(--color-primary);
      font-size: var(--text-xs);
      font-weight: 800;
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }
    h1 {
      margin: 0 0 0.75rem;
      font-family: var(--font-display, inherit);
      font-size: clamp(1.55rem, 3vw, 2.1rem);
      letter-spacing: -0.02em;
      line-height: 1.15;
    }
    h2 { margin: 0 0 0.65rem; font-size: var(--text-md); }
    .lead, .block p, .block li, .note {
      color: var(--color-text-secondary);
      line-height: var(--leading-body);
    }
    .lead { margin: 0 0 1.25rem; }
    .note { margin: 0.85rem 0 0; font-size: var(--text-sm); }
    .cta {
      display: flex;
      flex-wrap: wrap;
      gap: 0.65rem;
      margin: 0 0 2rem;
      align-items: center;
    }
    .cta a, .note a {
      color: var(--color-primary);
      font-weight: 700;
      text-decoration: none;
    }
    .block {
      margin: 0 0 1.5rem;
      padding: 1.2rem 1.25rem;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-sm);
    }
    ul, ol { margin: 0; padding-left: 1.15rem; display: grid; gap: 0.4rem; }
    .faq { display: grid; gap: 0.55rem; }
    details {
      border-top: 1px solid var(--color-border);
      padding-top: 0.55rem;
    }
    summary {
      cursor: pointer;
      font-weight: 700;
      color: var(--color-text);
    }
    details p { margin: 0.45rem 0 0.15rem; }
  `]
})
export class PublicSimulacroCalePage implements OnInit {
  readonly brand = BRAND;
  readonly faqs = FAQS;
  private readonly seo = inject(SeoService);

  ngOnInit(): void {
    this.seo.apply({
      title: PAGE_SEO.simulacroCale.title,
      description: PAGE_SEO.simulacroCale.description,
      path: '/simulacro-cale',
      index: true
    });
    this.seo.injectFaqJsonLd([...FAQS]);
  }
}
