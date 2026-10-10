import { Injectable, computed, inject, signal } from '@angular/core';
import { LegalApi, LegalConsent, MyLegalStatus } from './legal.api';

/** Estado de aceptación legal del usuario conectado; lo comparten el aviso y la página de aceptación. */
@Injectable({ providedIn: 'root' })
export class LegalStatusStore {
  private readonly api = inject(LegalApi);

  readonly status = signal<MyLegalStatus | null>(null);
  readonly pending = computed(() => this.status()?.documents.filter((d) => d.status !== 'ACCEPTED') ?? []);
  readonly needsAction = computed(() => this.pending().length > 0);

  load(): void {
    this.api.myStatus().subscribe({
      next: (status) => this.status.set(status),
      error: () => this.status.set(null)
    });
  }

  accept(consent: LegalConsent) {
    return this.api.accept(consent);
  }

  set(status: MyLegalStatus): void {
    this.status.set(status);
  }
}
