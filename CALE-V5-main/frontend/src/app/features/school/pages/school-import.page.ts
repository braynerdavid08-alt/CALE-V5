import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
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

type ImportMode = 'apprentices' | 'theory-exams';

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
  private readonly apprenticeApi = inject(ApprenticeApi);

  readonly acceptTypes = '.xlsx,.xls,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
  readonly mode = signal<ImportMode>('apprentices');
  readonly loading = signal(false);
  readonly committing = signal(false);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly file = signal<File | null>(null);
  readonly excelPreview = signal<ExcelImportPreview | null>(null);
  readonly excelCommit = signal<ExcelImportCommitResult | null>(null);

  setMode(value: ImportMode): void {
    this.mode.set(value);
    this.file.set(null);
    this.resetResults();
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file.set(input.files?.[0] ?? null);
    this.resetResults();
  }

  preview(): void {
    const f = this.file();
    if (!f) return;
    this.loading.set(true);
    this.error.set(null);
    this.ok.set(null);
    this.apprenticeApi.previewExcel(this.mode(), f).subscribe({
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
    const preview = this.excelPreview();
    if (!preview?.canCommit) return;
    this.committing.set(true);
    this.apprenticeApi.commitExcel(preview.previewId).subscribe({
      next: (dto) => {
        this.committing.set(false);
        this.excelCommit.set(dto);
        this.excelPreview.set(null);
        this.ok.set(`Importación: ${dto.updated} actualizados.`);
      },
      error: (err) => {
        this.committing.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  actionLabel(action: string): string {
    const map: Record<string, string> = {
      create: 'Crear',
      update: 'Actualizar',
      skip: 'Omitir',
      error: 'Error',
      pending: 'Pendiente'
    };
    return map[action] ?? action;
  }

  actionTone(action: string): 'success' | 'warning' | 'danger' | 'neutral' | 'primary' {
    if (action === 'create' || action === 'update') return 'success';
    if (action === 'skip' || action === 'pending') return 'warning';
    if (action === 'error') return 'danger';
    return 'neutral';
  }

  private resetResults(): void {
    this.excelPreview.set(null);
    this.excelCommit.set(null);
    this.error.set(null);
    this.ok.set(null);
  }
}
