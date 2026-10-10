import { Component, OnInit, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LegalStatusStore } from './legal-status.store';

/** Aviso no bloqueante para cuentas que aún no han aceptado la versión vigente de los documentos. */
@Component({
  selector: 'app-legal-banner',
  standalone: true,
  imports: [RouterLink],
  template: `
    @if (store.needsAction()) {
      <aside class="legal-banner" role="status">
        <span>
          <strong>{{ outdated() ? 'Actualizamos nuestros documentos legales.' : 'Revisa nuestros documentos legales.' }}</strong>
          Léelos y confirma tu aceptación para seguir usando Luz Verde con normalidad.
        </span>
        <a routerLink="/legal-pending">Revisar ahora</a>
      </aside>
    }
  `,
  styles: [`
    .legal-banner {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem 1rem;
      margin-bottom: 1rem;
      padding: 0.7rem 0.95rem;
      border: 1px solid var(--color-primary);
      border-radius: var(--radius-md);
      background: color-mix(in srgb, var(--color-primary) 8%, transparent);
      font-size: 0.9rem;
      line-height: 1.4;
    }
    .legal-banner a {
      font-weight: 700;
      color: var(--color-primary);
      white-space: nowrap;
    }
  `]
})
export class LegalBannerComponent implements OnInit {
  readonly store = inject(LegalStatusStore);
  readonly outdated = computed(() => this.store.pending().some((d) => d.status === 'OUTDATED'));

  ngOnInit(): void {
    this.store.load();
  }
}
