import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { mapApiError } from '../../../core/http/map-api-error';
import { env } from '../../../core/config/env';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';

interface SchedulingReportsStatus {
  moduleLabel: string;
  activationMessage: string;
  pendingCopy: string;
  enabled: boolean;
  allowManualDownload: boolean;
  allowManualWhatsAppSend: boolean;
  enableDailyFullReportJob: boolean;
  enableDailyChangesReportJob: boolean;
  whatsAppConfigured: boolean;
  dailyJobsArmed: boolean;
  scheduleInfo: {
    fullReportLocal: string;
    changesReportLocal: string;
    note: string;
  };
}

@Component({
  selector: 'app-school-scheduling-reports-page',
  standalone: true,
  imports: [UiErrorComponent, UiLoadingComponent, UiPageHeaderComponent],
  template: `
    <ui-page-header
      eyebrow="Escuela"
      title="Reportes de programación"
      subtitle="Excel del día con teoría, manejo y exámenes. Envíos automáticos y WhatsApp aún no activos." />

    <ui-error [message]="error()" />

    @if (loading()) {
      <ui-loading />
    } @else if (status()) {
      <section class="sr-banner" role="status">
        <p class="sr-banner__eyebrow">{{ status()!.activationMessage }}</p>
        <p class="sr-banner__copy">{{ status()!.pendingCopy }}</p>
      </section>

      <section class="sr-panel">
        <h2 class="sr-panel__title">Descarga manual</h2>
        <p class="sr-panel__hint">
          Genera el Excel de la programación del día (hora Colombia). No envía mensajes.
        </p>
        <label class="sr-check">
          <input type="checkbox" [checked]="onlyWithEnrollments()" (change)="onlyWithEnrollments.set($any($event.target).checked)" />
          Solo clases con estudiantes inscritos
        </label>
        <div class="sr-actions">
          <button
            type="button"
            class="btn primary"
            [disabled]="!status()!.allowManualDownload || downloading()"
            (click)="downloadToday()">
            {{ downloading() === 'today' ? 'Generando…' : 'Solicitar reporte de hoy' }}
          </button>
          <button
            type="button"
            class="btn"
            [disabled]="!status()!.allowManualDownload || downloading()"
            (click)="downloadTomorrow()">
            {{ downloading() === 'tomorrow' ? 'Generando…' : 'Reporte de mañana' }}
          </button>
        </div>
        @if (!status()!.allowManualDownload) {
          <p class="sr-muted">La descarga manual está deshabilitada en el servidor.</p>
        }
      </section>

      <section class="sr-panel sr-panel--muted">
        <h2 class="sr-panel__title">WhatsApp</h2>
        <p class="sr-panel__hint">
          Se habilitará cuando Luz Verde active la integración con Meta Cloud API.
        </p>
        <button type="button" class="btn" disabled>
          Enviar por WhatsApp — Próximamente
        </button>
      </section>

      <section class="sr-panel sr-panel--muted">
        <h2 class="sr-panel__title">Envíos programados (informativo)</h2>
        <ul class="sr-schedule">
          <li>
            <strong>14:00</strong> Colombia — reporte completo del día
            <span class="sr-tag">Pendiente de activación</span>
          </li>
          <li>
            <strong>17:00</strong> Colombia — novedades desde el corte de las 14:00
            <span class="sr-tag">Pendiente de activación</span>
          </li>
        </ul>
        <p class="sr-muted">{{ status()!.scheduleInfo.note }}</p>
      </section>
    }
  `,
  styles: `
    .sr-banner {
      margin: 0 0 1.25rem;
      padding: 1rem 1.15rem;
      border-radius: 12px;
      background: color-mix(in srgb, var(--lv-accent, #1b7a4e) 12%, transparent);
      border: 1px solid color-mix(in srgb, var(--lv-accent, #1b7a4e) 28%, transparent);
    }
    .sr-banner__eyebrow {
      margin: 0 0 0.35rem;
      font-weight: 700;
      font-size: 0.85rem;
      letter-spacing: 0.02em;
      text-transform: uppercase;
      color: var(--lv-accent, #1b7a4e);
    }
    .sr-banner__copy {
      margin: 0;
      line-height: 1.45;
      color: var(--lv-text, inherit);
    }
    .sr-panel {
      margin: 0 0 1.25rem;
      padding: 1rem 0;
      border-top: 1px solid color-mix(in srgb, currentColor 12%, transparent);
    }
    .sr-panel--muted {
      opacity: 0.92;
    }
    .sr-panel__title {
      margin: 0 0 0.35rem;
      font-size: 1.05rem;
    }
    .sr-panel__hint {
      margin: 0 0 0.85rem;
      color: color-mix(in srgb, currentColor 72%, transparent);
      line-height: 1.4;
    }
    .sr-check {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0 0 0.85rem;
      font-size: 0.92rem;
      cursor: pointer;
    }
    .sr-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.65rem;
    }
    .sr-schedule {
      margin: 0 0 0.75rem;
      padding-left: 1.1rem;
      line-height: 1.55;
    }
    .sr-tag {
      display: inline-block;
      margin-left: 0.35rem;
      font-size: 0.75rem;
      font-weight: 600;
      color: var(--lv-accent, #1b7a4e);
    }
    .sr-muted {
      margin: 0.5rem 0 0;
      font-size: 0.9rem;
      color: color-mix(in srgb, currentColor 65%, transparent);
    }
    .btn {
      appearance: none;
      border: 1px solid color-mix(in srgb, currentColor 22%, transparent);
      background: transparent;
      color: inherit;
      border-radius: 10px;
      padding: 0.55rem 0.95rem;
      font: inherit;
      cursor: pointer;
    }
    .btn.primary {
      background: var(--lv-accent, #1b7a4e);
      border-color: transparent;
      color: #fff;
    }
    .btn:disabled {
      opacity: 0.55;
      cursor: not-allowed;
    }
  `
})
export class SchoolSchedulingReportsPage implements OnInit {
  private readonly http = inject(HttpClient);
  readonly status = signal<SchedulingReportsStatus | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly downloading = signal<'today' | 'tomorrow' | null>(null);
  readonly onlyWithEnrollments = signal(false);

  ngOnInit(): void {
    this.http
      .get<SchedulingReportsStatus>(`${env.apiUrl}/api/school/scheduling-reports/status`)
      .subscribe({
        next: (s) => {
          this.status.set(s);
          this.loading.set(false);
        },
        error: (err) => {
          this.loading.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }

  downloadToday(): void {
    this.download('today', 'generate/today');
  }

  downloadTomorrow(): void {
    this.download('tomorrow', 'generate/tomorrow');
  }

  private download(kind: 'today' | 'tomorrow', path: string): void {
    if (this.downloading()) {
      return;
    }
    this.downloading.set(kind);
    this.error.set(null);
    const q = this.onlyWithEnrollments() ? '?onlyWithEnrollments=true' : '';
    this.http
      .post(`${env.apiUrl}/api/school/scheduling-reports/${path}${q}`, {}, { responseType: 'blob' })
      .subscribe({
        next: (blob) => {
          this.downloading.set(null);
          try {
            const d = new Date();
            const key = [
              d.getFullYear(),
              `${d.getMonth() + 1}`.padStart(2, '0'),
              `${d.getDate()}`.padStart(2, '0')
            ].join('-');
            localStorage.setItem(`cale.school.reportDownloaded.${key}`, '1');
          } catch {
            /* ignore */
          }
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          const suffix = this.onlyWithEnrollments() ? '-inscritos' : '';
          a.download =
            kind === 'today'
              ? `programacion-hoy${suffix}.xlsx`
              : `programacion-manana${suffix}.xlsx`;
          a.click();
          URL.revokeObjectURL(url);
        },
        error: async (err) => {
          this.downloading.set(null);
          this.error.set(await this.blobError(err));
        }
      });
  }

  private async blobError(err: unknown): Promise<string> {
    const httpErr = err as { error?: Blob; message?: string };
    if (httpErr?.error instanceof Blob) {
      try {
        const text = await httpErr.error.text();
        const parsed = JSON.parse(text) as { error?: string };
        if (parsed.error) {
          return parsed.error;
        }
      } catch {
        /* fall through */
      }
    }
    return mapApiError(err);
  }
}
