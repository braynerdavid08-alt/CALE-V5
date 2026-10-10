import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../core/http/map-api-error';
import { UiButtonComponent } from '../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../shared/ui/ui-success.component';
import { CONSENT_LABELS, LegalAcceptance, LegalApi, LegalConsent, LegalDocKey } from './legal.api';
import { LegalStatusStore } from './legal-status.store';

const CONSENT_FIELD: Partial<Record<LegalDocKey, keyof LegalConsent>> = {
  terms: 'acceptTerms',
  privacy: 'acknowledgePrivacy',
  'data-processing': 'authorizeDataProcessing',
  'school-terms': 'acceptSchoolTerms'
};

const STATUS_LABEL: Record<string, string> = {
  ACCEPTED: 'Aceptado',
  OUTDATED: 'Hay una versión nueva',
  NOT_VERIFIED: 'Sin aceptar'
};

@Component({
  selector: 'app-legal-pending-page',
  standalone: true,
  imports: [DatePipe, RouterLink, UiButtonComponent, UiCardComponent, UiErrorComponent, UiPageHeaderComponent, UiSuccessComponent],
  template: `
    <ui-page-header
      eyebrow="Tu cuenta"
      title="Documentos legales"
      subtitle="Lee cada documento y marca las casillas. Cada aceptación queda registrada con su versión y fecha." />

    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />

    <ui-card>
      <ul class="docs">
        @for (d of store.status()?.documents ?? []; track d.key) {
          <li>
            <div class="doc-head">
              <a [routerLink]="['/legal', d.key]" target="_blank">{{ d.title }}</a>
              <span class="pill" [class.ok]="d.status === 'ACCEPTED'">{{ statusLabel(d.status) }}</span>
            </div>
            <small class="muted">
              Versión vigente {{ d.currentVersion ?? '—' }}
              @if (d.acceptedVersion) { · aceptaste la {{ d.acceptedVersion }} el {{ d.acceptedAt | date: 'dd/MM/yyyy' }} }
            </small>
            @if (d.status !== 'ACCEPTED') {
              <label class="check">
                <input type="checkbox" [checked]="checked()[d.key]" (change)="toggle(d.key, $any($event.target).checked)" />
                <span>{{ label(d.key) }} (versión {{ d.currentVersion }}).</span>
              </label>
            }
          </li>
        }
      </ul>

      @if (store.needsAction()) {
        <ui-button type="button" [disabled]="busy() || !allChecked()" (click)="accept()">Confirmar</ui-button>
      } @else if (store.status()) {
        <p class="muted">Tienes al día todos los documentos que aplican a tu cuenta.</p>
      }
      <p class="muted small">
        Los documentos están en borrador y pueden cambiar. Si hay un cambio importante te pediremos aceptarlo de nuevo.
        La Política de uso de IA está disponible en <a routerLink="/legal/ai-policy" target="_blank">este enlace</a>.
      </p>
    </ui-card>

    @if (history().length > 0) {
      <ui-card>
        <h2>Tu historial</h2>
        <ul class="history">
          @for (h of history(); track h.id) {
            <li>{{ h.createdAt | date: 'dd/MM/yyyy HH:mm' }} · {{ titleFor(h.documentKey) }} · versión {{ h.version }}</li>
          }
        </ul>
      </ui-card>
    }
  `,
  styles: [`
    .docs { list-style: none; margin: 0 0 1rem; padding: 0; display: grid; gap: 0.85rem; }
    .docs li { padding-bottom: 0.85rem; border-bottom: 1px solid var(--color-border); }
    .doc-head { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; justify-content: space-between; }
    .doc-head a { font-weight: 700; color: var(--color-primary); }
    .pill {
      font-size: 0.75rem;
      padding: 0.15rem 0.55rem;
      border-radius: 999px;
      border: 1px solid #c99400;
      color: var(--color-text);
    }
    .pill.ok { border-color: var(--color-success, #2e7d32); }
    .check { display: flex; gap: 0.5rem; align-items: flex-start; margin-top: 0.5rem; cursor: pointer; line-height: 1.4; }
    .check input { margin-top: 0.2rem; width: 1rem; height: 1rem; flex: none; }
    .muted { color: var(--color-text-secondary); }
    .small { font-size: 0.8rem; margin-top: 1rem; }
    h2 { margin: 0 0 0.5rem; font-size: 1.05rem; }
    .history { margin: 0; padding-left: 1.1rem; display: grid; gap: 0.3rem; font-size: 0.88rem; }
  `]
})
export class LegalPendingPage implements OnInit {
  readonly store = inject(LegalStatusStore);
  private readonly api = inject(LegalApi);

  readonly checked = signal<Record<string, boolean>>({});
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly history = signal<LegalAcceptance[]>([]);
  readonly allChecked = computed(() => this.store.pending().every((d) => this.checked()[d.key]));

  ngOnInit(): void {
    this.store.load();
    this.loadHistory();
  }

  statusLabel(status: string): string {
    return STATUS_LABEL[status] ?? status;
  }

  label(key: LegalDocKey): string {
    return CONSENT_LABELS[key];
  }

  titleFor(key: LegalDocKey): string {
    return this.store.status()?.documents.find((d) => d.key === key)?.title ?? key;
  }

  toggle(key: string, value: boolean): void {
    this.checked.update((c) => ({ ...c, [key]: value }));
  }

  accept(): void {
    const consent: LegalConsent = { acceptTerms: false, acknowledgePrivacy: false, authorizeDataProcessing: false, acceptSchoolTerms: false };
    for (const d of this.store.pending()) {
      const field = CONSENT_FIELD[d.key];
      if (field) {
        consent[field] = !!this.checked()[d.key];
      }
    }
    this.busy.set(true);
    this.error.set(null);
    this.store.accept(consent).subscribe({
      next: (status) => {
        this.busy.set(false);
        this.store.set(status);
        this.checked.set({});
        this.ok.set('Gracias. Tu aceptación quedó registrada.');
        this.loadHistory();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private loadHistory(): void {
    this.api.myHistory().subscribe({ next: (rows) => this.history.set(rows), error: () => this.history.set([]) });
  }
}
