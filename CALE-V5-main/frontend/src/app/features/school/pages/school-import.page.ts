import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiStatComponent } from '../../../shared/ui/ui-stat.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';

interface ImportRowPreview {
  lineNumber: number;
  name: string;
  email: string;
  role: string;
  action: string;
  severity: string;
  code?: string | null;
  message?: string | null;
}

interface ImportPreview {
  previewId: string;
  fileName: string;
  totalRows: number;
  createCount: number;
  attachCount: number;
  skipCount: number;
  errorCount: number;
  canCommit: boolean;
  blockingReason?: string | null;
  rows: ImportRowPreview[];
}

interface ImportCommitResult {
  previewId: string;
  created: number;
  attached: number;
  skipped: number;
  failed: number;
  credentials: { name: string; email: string; role: string; temporaryPassword: string }[];
  results: ImportRowPreview[];
  credentialsCsv: string;
}

@Component({
  selector: 'app-school-import-page',
  standalone: true,
  imports: [
    RouterLink,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiStatComponent,
    UiSuccessComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Escuela"
      title="Importar usuarios"
      subtitle="Sube un CSV de estudiantes e instructores. Primero verás el preview; luego confirmas." />

    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />

    <div class="actions-top">
      <a routerLink="/school/users"><ui-button type="button" variant="secondary">Volver a usuarios</ui-button></a>
      <ui-button type="button" variant="secondary" (click)="downloadTemplate()">Descargar plantilla CSV</ui-button>
    </div>

    <ui-card>
      <h2>1. Archivo</h2>
      <p class="muted">Columnas: <code>nombre,email,rol</code> (Student o Teacher). Máximo 2000 filas.</p>
      <input type="file" accept=".csv,text/csv" (change)="onFile($event)" />
      <div class="row">
        <ui-button type="button" [disabled]="!file() || loading()" (click)="preview()">
          Analizar CSV
        </ui-button>
      </div>
    </ui-card>

    @if (loading()) {
      <ui-loading />
    }

    @if (previewData()) {
      <div class="grid-stats">
        <ui-stat label="Filas" [value]="previewData()!.totalRows" />
        <ui-stat label="Crear" [value]="previewData()!.createCount" tone="success" />
        <ui-stat label="Vincular" [value]="previewData()!.attachCount" tone="primary" />
        <ui-stat label="Omitir" [value]="previewData()!.skipCount" />
        <ui-stat label="Errores" [value]="previewData()!.errorCount" tone="warning" />
      </div>

      @if (previewData()!.blockingReason) {
        <ui-error [message]="previewData()!.blockingReason!" />
      }

      <ui-card>
        <div class="head">
          <h2>2. Vista previa — {{ previewData()!.fileName }}</h2>
          <ui-button
            type="button"
            [disabled]="!previewData()!.canCommit || committing()"
            (click)="commit()">
            Confirmar importación
          </ui-button>
        </div>
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>#</th>
                <th>Nombre</th>
                <th>Email</th>
                <th>Rol</th>
                <th>Acción</th>
                <th>Detalle</th>
              </tr>
            </thead>
            <tbody>
              @for (row of previewData()!.rows; track row.lineNumber + row.email) {
                <tr [attr.data-sev]="row.severity">
                  <td>{{ row.lineNumber }}</td>
                  <td>{{ row.name }}</td>
                  <td>{{ row.email }}</td>
                  <td>{{ row.role }}</td>
                  <td><ui-badge [tone]="actionTone(row.action)">{{ actionLabel(row.action) }}</ui-badge></td>
                  <td>{{ row.message || '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </ui-card>
    }

    @if (commitResult()) {
      <ui-card>
        <h2>3. Resultado</h2>
        <p>
          Creados {{ commitResult()!.created }},
          vinculados {{ commitResult()!.attached }},
          omitidos {{ commitResult()!.skipped }},
          fallidos {{ commitResult()!.failed }}.
        </p>
        @if (commitResult()!.credentials.length) {
          <p class="muted">
            Descarga las contraseñas temporales y entrégalas a cada usuario.
            Deberán cambiarlas al primer acceso.
          </p>
          <ui-button type="button" (click)="downloadCredentials()">Descargar contraseñas CSV</ui-button>
        }
      </ui-card>
    }
  `,
  styles: [`
    .actions-top { display: flex; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 1rem; }
    .muted { color: var(--color-text-secondary); }
    .row { margin-top: 0.75rem; }
    h2 { margin: 0 0 0.75rem; font-size: var(--text-lg); }
    .head { display: flex; justify-content: space-between; gap: 1rem; align-items: center; flex-wrap: wrap; margin-bottom: 0.75rem; }
    .head h2 { margin: 0; }
    .table-wrap { overflow: auto; max-height: 28rem; }
    table { width: 100%; border-collapse: collapse; font-size: var(--text-sm); }
    th, td { text-align: left; padding: 0.45rem 0.5rem; border-bottom: 1px solid var(--color-border); vertical-align: top; }
    tr[data-sev='error'] td { color: var(--color-danger); }
    tr[data-sev='warning'] td { color: var(--color-warning); }
    code { font-size: 0.9em; }
  `]
})
export class SchoolImportPage {
  private readonly http = inject(HttpClient);
  readonly loading = signal(false);
  readonly committing = signal(false);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly file = signal<File | null>(null);
  readonly previewData = signal<ImportPreview | null>(null);
  readonly commitResult = signal<ImportCommitResult | null>(null);

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file.set(input.files?.[0] ?? null);
    this.previewData.set(null);
    this.commitResult.set(null);
    this.error.set(null);
    this.ok.set(null);
  }

  downloadTemplate(): void {
    this.http.get(`${env.apiUrl}/api/school/imports/template`, {
      responseType: 'blob'
    }).subscribe({
      next: (blob) => this.saveBlob(blob, 'cale-import-usuarios.csv'),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  preview(): void {
    const f = this.file();
    if (!f) return;
    this.loading.set(true);
    this.error.set(null);
    this.ok.set(null);
    this.commitResult.set(null);
    const body = new FormData();
    body.append('file', f, f.name);
    this.http.post<ImportPreview>(`${env.apiUrl}/api/school/imports/preview`, body).subscribe({
      next: (dto) => {
        this.previewData.set(dto);
        this.loading.set(false);
        this.ok.set(
          dto.canCommit
            ? 'Preview listo. Revisa la tabla y confirma la importación.'
            : null
        );
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  commit(): void {
    const preview = this.previewData();
    if (!preview?.canCommit) return;
    this.committing.set(true);
    this.error.set(null);
    this.http.post<ImportCommitResult>(
      `${env.apiUrl}/api/school/imports/${preview.previewId}/commit`,
      {}
    ).subscribe({
      next: (dto) => {
        this.committing.set(false);
        this.commitResult.set(dto);
        this.previewData.set(null);
        this.ok.set(
          `Importación terminada: ${dto.created} creados, ${dto.attached} vinculados.`
        );
      },
      error: (err) => {
        this.committing.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  downloadCredentials(): void {
    const result = this.commitResult();
    if (!result?.credentialsCsv) return;
    const blob = new Blob([result.credentialsCsv], { type: 'text/csv;charset=utf-8' });
    this.saveBlob(blob, 'cale-credenciales-temporales.csv');
  }

  actionLabel(action: string): string {
    if (action === 'create') return 'Crear';
    if (action === 'attach') return 'Vincular';
    if (action === 'skip') return 'Omitir';
    if (action === 'error') return 'Error';
    return action;
  }

  actionTone(action: string): 'success' | 'warning' | 'danger' | 'neutral' | 'primary' {
    if (action === 'create') return 'success';
    if (action === 'attach') return 'primary';
    if (action === 'skip') return 'warning';
    if (action === 'error') return 'danger';
    return 'neutral';
  }

  private saveBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }
}
