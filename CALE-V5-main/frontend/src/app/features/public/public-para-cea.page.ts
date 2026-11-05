import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BRAND } from '../../core/brand';
import { PAGE_SEO } from '../../core/seo/page-seo';
import { SeoService } from '../../core/seo/seo.service';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';

@Component({
  selector: 'app-public-para-cea-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent],
  template: `
    <div class="page">
      <header class="head">
        <p class="eyebrow">Para CEA y escuelas de conducción</p>
        <h1>Gestiona tu Centro de Enseñanza Automovilística con {{ brand.name }}</h1>
        <p class="lead">
          {{ brand.name }} es una plataforma tecnológica para escuelas de manejo, escuelas de conducción
          y CEA en Colombia: operación académica, formación vial y preparación de aprendices en un solo lugar.
        </p>
        <div class="cta">
          <ui-button routerLink="/register-school" type="button">Registrar mi CEA</ui-button>
          <ui-button routerLink="/contacto" type="button" variant="secondary">Solicitar información</ui-button>
        </div>
      </header>

      <section class="block">
        <h2>Qué problemas resuelve</h2>
        <p>
          Coordinar estudiantes, instructores, agenda de clases teóricas y prácticas, exámenes e información
          operativa suele fragmentarse en hojas de cálculo y WhatsApp. {{ brand.name }} centraliza esos procesos
          para que tu escuela opere con claridad.
        </p>
      </section>

      <section class="block">
        <h2>Herramientas para tu CEA</h2>
        <ul>
          <li>Administración de estudiantes e instructores</li>
          <li>Programación académica y gestión de horarios</li>
          <li>Clases teóricas y prácticas</li>
          <li>Exámenes y seguimiento de formación</li>
          <li>Simulacro teórico para aprendices</li>
          <li>Informes e indicadores de operación</li>
          <li>Presencia comercial opcional en el directorio público (con plan activo y autorización de publicación)</li>
        </ul>
      </section>

      <section class="block">
        <h2>Software para escuelas de conducción en Colombia</h2>
        <p>
          Si buscas software para CEA, plataforma para escuelas de manejo o un sistema de gestión académica
          para Centros de Enseñanza Automovilística, {{ brand.name }} está pensado para ese contexto operativo.
          No sustituimos tu escuela: potenciamos cómo la administras y formas.
        </p>
        <div class="cta">
          <a routerLink="/escuelas">Ver directorio de escuelas vinculadas</a>
          <a routerLink="/nosotros">Conocer la plataforma</a>
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
    .lead, .block p, .block li {
      color: var(--color-text-secondary);
      line-height: var(--leading-body);
    }
    .lead { margin: 0 0 1.25rem; }
    .cta {
      display: flex;
      flex-wrap: wrap;
      gap: 0.65rem;
      margin: 0 0 2rem;
      align-items: center;
    }
    .cta a {
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
    ul { margin: 0; padding-left: 1.15rem; display: grid; gap: 0.4rem; }
  `]
})
export class PublicParaCeaPage implements OnInit {
  readonly brand = BRAND;
  private readonly seo = inject(SeoService);

  ngOnInit(): void {
    this.seo.apply({
      title: PAGE_SEO.forCea.title,
      description: PAGE_SEO.forCea.description,
      path: '/para-cea',
      index: true
    });
  }
}
