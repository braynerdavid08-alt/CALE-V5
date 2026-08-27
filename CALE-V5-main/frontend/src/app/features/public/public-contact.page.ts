import { Component, OnInit, inject, signal } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { mapApiError } from '../../core/http/map-api-error';
import { brandPageTitle } from '../../core/brand';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiIconComponent } from '../../shared/ui/ui-icon.component';
import { UiLoadingComponent } from '../../shared/ui/ui-loading.component';
import { PublicHomeApi } from './public-home.api';

@Component({
  selector: 'app-public-contact-page',
  standalone: true,
  imports: [UiErrorComponent, UiIconComponent, UiLoadingComponent],
  template: `
    <div class="page">
      <header class="head">
        <p class="eyebrow">Contacto</p>
        <h1>Habla con el equipo de Mi CALE</h1>
        <p class="lead">Estamos para ayudarte con escuelas, cuentas y formación.</p>
      </header>

      @if (loading()) {
        <ui-loading />
      } @else if (error()) {
        <ui-error [message]="error()" />
      } @else {
        <div class="cards">
          <article class="card">
            <div class="icon" aria-hidden="true"><ui-icon name="bell" /></div>
            <h2>Correo</h2>
            @if (email()) {
              <a class="value" [href]="'mailto:' + email()">{{ email() }}</a>
            } @else {
              <p class="muted">Sin correo publicado</p>
            }
          </article>
          <article class="card">
            <div class="icon" aria-hidden="true"><ui-icon name="users" /></div>
            <h2>Teléfono</h2>
            @if (phone()) {
              <a class="value" [href]="'tel:' + phone()">{{ phone() }}</a>
            } @else {
              <p class="muted">Sin teléfono publicado</p>
            }
          </article>
        </div>
      }
    </div>
  `,
  styles: [`
    .page {
      width: min(800px, calc(100% - 2rem));
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
    h1 { margin: 0 0 0.5rem; }
    .lead {
      margin: 0 0 1.75rem;
      color: var(--color-text-secondary);
      line-height: var(--leading-body);
    }
    .cards {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1rem;
    }
    .card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 1.35rem;
      box-shadow: var(--shadow-sm);
      display: grid;
      gap: 0.45rem;
    }
    .icon {
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 12px;
      background: var(--color-primary-soft);
      color: var(--color-primary);
      display: grid;
      place-items: center;
    }
    h2 { margin: 0; font-size: var(--text-sm); color: var(--color-text-secondary); }
    .value {
      color: var(--color-text);
      font-weight: 700;
      font-size: var(--text-md);
      text-decoration: none;
      word-break: break-all;
    }
    .value:hover { color: var(--color-primary); }
    .muted { margin: 0; color: var(--color-text-secondary); }
    @media (max-width: 600px) {
      .cards { grid-template-columns: 1fr; }
    }
  `]
})
export class PublicContactPage implements OnInit {
  private readonly api = inject(PublicHomeApi);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly email = signal('');
  readonly phone = signal('');

  ngOnInit(): void {
    this.title.setTitle(brandPageTitle('Contacto'));
    this.meta.updateTag({
      name: 'description',
      content: 'Contacto del equipo Mi CALE para formación vial.'
    });

    this.api.getHome().subscribe({
      next: (data) => {
        this.email.set(data.contactEmail?.trim() || '');
        this.phone.set(data.contactPhone?.trim() || '');
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }
}
