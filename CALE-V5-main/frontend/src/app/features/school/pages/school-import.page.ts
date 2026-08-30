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
import { ApprenticeApi, ExcelImportCommitResult, ExcelImportPreview } from '../api/apprentice.api';

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

type ImportMode = 'users' | 'apprentices' | 'theory-exams';

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
  templateUrl: './school-import.page.html',
  styleUrl: './school-import.page.css'
})
export class SchoolImportPage {
  private readonly http = inject(HttpClient);
  private readonly apprenticeApi = inject(ApprenticeApi);

  readonly mode = signal<ImportMode>('apprentices');
  readonly loading = signal(false);
  readonly committing = signal(false);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly file = signal<File | null>(null);
  readonly csvPreview = signal<ImportPreview | null>(null);
  readonly excelPreview = signal<ExcelImportPreview | null>(null);
  readonly csvCommit = signal<ImportCommitResult | null>(null);
  readonly excelCommit = signal<ExcelImportCommitResult | null>(null);

  setMode(value: ImportMode): void {
    this.mode.set(value);
    this.file.set(null);
    this.csvPreview.set(null);
    this.excelPreview.set(null);
    this.csvCommit.set(null);
    this.excelCommit.set(null);
    this.error.set(null);
    this.ok.set(null);
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file.set(input.files?.[0] ?? null);
    this.csvPreview.set(null);
    this.excelPreview.set(null);
    this.csvCommit.set(null);
    this.excelCommit.set(null);
    this.error.set(null);
    this.ok.set(null);
  }

  acceptTypes(): string {
    return this.mode() === 'users'
      ? '.csv,text/csv'
      : '.xlsx,.xls,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
  }

  downloadTemplate(): void {
    this.http.get(`${env.apiUrl}/api/school/imports/template`, { responseType: 'blob' }).subscribe({
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

    if (this.mode() === 'users') {
      const body = new FormData();
      body.append('file', f, f.name);
      this.http.post<ImportPreview>(`${env.apiUrl}/api/school/imports/preview`, body).subscribe({
        next: (dto) => {
          this.csvPreview.set(dto);
          this.loading.set(false);
          this.ok.set(dto.canCommit ? 'Preview listo. Revisa y confirma.' : null);
        },
        error: (err) => {
          this.loading.set(false);
          this.error.set(mapApiError(err));
        }
      });
      return;
    }

    const importType = this.mode() === 'apprentices' ? 'apprentices' : 'theory-exams';
    this.apprenticeApi.previewExcel(importType, f).subscribe({
      next: (dto) => {
        this.excelPreview.set(dto);
        this.loading.set(false);
        this.ok.set(dto.canCommit ? 'Preview listo. Revisa y confirma.' : null);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  commit(): void {
    if (this.mode() === 'users') {
      const preview = this.csvPreview();
      if (!preview?.canCommit) return;
      this.committing.set(true);
      this.http.post<ImportCommitResult>(
        `${env.apiUrl}/api/school/imports/${preview.previewId}/commit`,
        {}
      ).subscribe({
        next: (dto) => {
          this.committing.set(false);
          this.csvCommit.set(dto);
          this.csvPreview.set(null);
          this.ok.set(`Importación: ${dto.created} creados, ${dto.attached} vinculados.`);
        },
        error: (err) => {
          this.committing.set(false);
          this.error.set(mapApiError(err));
        }
      });
      return;
    }

    const preview = this.excelPreview();
    if (!preview?.canCommit) return;
    this.committing.set(true);
    this.apprenticeApi.commitExcel(preview.previewId).subscribe({
      next: (dto) => {
        this.committing.set(false);
        this.excelCommit.set(dto);
        this.excelPreview.set(null);
        this.ok.set(`Importación: ${dto.created} nuevos, ${dto.updated} actualizados.`);
      },
      error: (err) => {
        this.committing.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  downloadCredentials(): void {
    const result = this.csvCommit();
    if (!result?.credentialsCsv) return;
    this.saveBlob(new Blob([result.credentialsCsv], { type: 'text/csv;charset=utf-8' }), 'cale-credenciales.csv');
  }

  downloadExcelCredentials(): void {
    const result = this.excelCommit();
    if (!result?.credentialsCsv) return;
    this.saveBlob(new Blob([result.credentialsCsv], { type: 'text/csv;charset=utf-8' }), 'cale-aprendices-credenciales.csv');
  }

  actionLabel(action: string): string {
    const map: Record<string, string> = {
      create: 'Crear',
      update: 'Actualizar',
      attach: 'Vincular',
      skip: 'Omitir',
      error: 'Error',
      pending: 'Pendiente'
    };
    return map[action] ?? action;
  }

  actionTone(action: string): 'success' | 'warning' | 'danger' | 'neutral' | 'primary' {
    if (action === 'create' || action === 'update') return 'success';
    if (action === 'attach') return 'primary';
    if (action === 'skip' || action === 'pending') return 'warning';
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
