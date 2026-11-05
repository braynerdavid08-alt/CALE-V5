import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BRAND } from '../../core/brand';
import { PAGE_SEO } from '../../core/seo/page-seo';
import { SeoService } from '../../core/seo/seo.service';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';

@Component({
  selector: 'app-public-estudiantes-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent],
  template: `
    <div class="page">
      <header class="head">
        <p class="eyebrow">Para estudiantes y futuros conductores</p>
        <h1>Comienza tu camino hacia una conducción segura con {{ brand.name }}</h1>
        <p class="lead">
          {{ brand.name }} te conecta con herramientas de formación vial, preparación para evaluaciones
          y escuelas de conducción vinculadas a la plataforma.
        </p>
        <div class="cta">
          <ui-button routerLink="/simulacro-cale" type="button">Practicar examen CALE</ui-button>
          <ui-button routerLink="/register" type="button" variant="secondary">Crear cuenta</ui-button>
        </div>
      </header>

      <section class="block">
        <h2>Formación vial y preparación</h2>
        <p>
          Estudia normas de tránsito, señales viales y seguridad vial con recursos de la plataforma.
          Practica tu examen teórico CALE, refuerza lo que más te cuesta y sigue el ritmo de tu escuela
          cuando estés vinculado a un CEA.
        </p>
      </section>

      <section class="block">
        <h2>Encuentra una escuela de conducción</h2>
        <p>
          Explora el directorio de escuelas de manejo y Centros de Enseñanza Automovilística que autorizaron
          su publicación comercial en {{ brand.name }}. Solo aparecen fichas elegibles con membresía activa
          y publicación voluntaria.
        </p>
        <div class="cta">
          <a routerLink="/escuelas">Ver escuelas vinculadas</a>
          <a routerLink="/cursos">Conocer la malla vial</a>
        </div>
      </section>

      <section class="block">
        <h2>Cómo empezar</h2>
        <ol>
          <li>Crea tu cuenta de estudiante.</li>
          <li>Vincula tu CEA o explora el directorio público.</li>
          <li>Practica el simulacro CALE y avanza en tu formación.</li>
        </ol>
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
    ol { margin: 0; padding-left: 1.15rem; display: grid; gap: 0.4rem; }
  `]
})
export class PublicEstudiantesPage implements OnInit {
  readonly brand = BRAND;
  private readonly seo = inject(SeoService);

  ngOnInit(): void {
    this.seo.apply({
      title: PAGE_SEO.students.title,
      description: PAGE_SEO.students.description,
      path: '/estudiantes',
      index: true
    });
  }
}
