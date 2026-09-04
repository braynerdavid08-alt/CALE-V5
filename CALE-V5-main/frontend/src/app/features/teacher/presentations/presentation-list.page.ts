import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { SessionStore } from '../../../core/auth/session.store';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { LiveApi } from '../../live/api/live.api';
import { TeacherApi } from '../api/teacher.api';
import { PresentationApi } from './presentation.api';
import {
  PRESENTATION_CATEGORIES,
  PRESENTATION_IMPORT_MAX_BYTES,
  PresentationListItem,
  TEMPLATE_OPTIONS
} from './presentation.models';

@Component({
  selector: 'app-presentation-list-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent
  ],
  templateUrl: './presentation-list.page.html',
  styleUrl: './presentation-list.page.css'
})
export class PresentationListPage implements OnInit {
  private readonly api = inject(PresentationApi);
  private readonly liveApi = inject(LiveApi);
  private readonly teacherApi = inject(TeacherApi);
  private readonly router = inject(Router);
  readonly session = inject(SessionStore);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<PresentationListItem[]>([]);
  readonly creating = signal(false);
  readonly importing = signal(false);
  readonly liveStarting = signal(false);
  readonly showCreate = signal(false);

  title = '';
  description = '';
  category: string = PRESENTATION_CATEGORIES[0];
  templateKey = 'cover';
  importTitle = '';
  importFile: File | null = null;
  readonly categories = PRESENTATION_CATEGORIES;
  readonly templates = TEMPLATE_OPTIONS;

  ngOnInit(): void {
    this.reload();
  }

  @HostListener('document:visibilitychange')
  onVisibilityChange(): void {
    if (document.visibilityState === 'visible' && !this.loading()) {
      this.reload();
    }
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.list().subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
        if (items.length === 0) {
          this.showCreate.set(true);
        }
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }

  create(): void {
    if (!this.title.trim() || this.creating()) {
      return;
    }
    this.creating.set(true);
    this.api
      .create({
        title: this.title.trim(),
        description: this.description.trim() || null,
        category: this.category,
        templateKey: this.templateKey
      })
      .subscribe({
        next: (detail) => {
          this.creating.set(false);
          void this.router.navigate(['/teacher/presentations', detail.id, 'edit']);
        },
        error: (err) => {
          this.creating.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }

  openCreate(): void {
    this.showCreate.set(true);
  }

  onImportFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    if (file && file.size > PRESENTATION_IMPORT_MAX_BYTES) {
      this.error.set('El archivo debe pesar 200 MB o menos.');
      this.importFile = null;
      input.value = '';
      return;
    }
    this.importFile = file;
    if (this.importFile && !this.importTitle.trim()) {
      this.importTitle = this.importFile.name.replace(/\.(xlsx|xls|docx|pptx)$/i, '');
    }
  }

  downloadTemplate(format: 'xlsx' | 'docx'): void {
    this.api.downloadImportTemplate(format).subscribe({
      next: (blob) => this.saveBlob(blob, format === 'xlsx' ? 'cale-presentacion-plantilla.xlsx' : 'cale-presentacion-plantilla.docx'),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  importPresentation(): void {
    if (!this.importFile || this.importing()) return;
    this.importing.set(true);
    this.error.set(null);
    this.api.importFile(this.importFile, {
      title: this.importTitle.trim() || undefined,
      category: this.category
    }).subscribe({
      next: (detail) => {
        this.importing.set(false);
        void this.router.navigate(['/teacher/presentations', detail.id, 'edit'], {
          state: { importSummary: true }
        });
      },
      error: (err) => {
        this.importing.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  exportPresentation(item: PresentationListItem, format: 'xlsx' | 'docx' | 'pptx', ev: Event): void {
    ev.preventDefault();
    ev.stopPropagation();
    const ext = format === 'xlsx' ? 'xlsx' : format === 'docx' ? 'docx' : 'pptx';
    this.api.exportFile(item.id, format).subscribe({
      next: (blob) => this.saveBlob(blob, `${item.title}.${ext}`),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private saveBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  duplicate(item: PresentationListItem, ev: Event): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.api.duplicate(item.id).subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  presentLive(item: PresentationListItem, ev: Event): void {
    ev.preventDefault();
    ev.stopPropagation();
    if (this.liveStarting()) {
      return;
    }
    this.liveStarting.set(true);
    this.error.set(null);
    this.teacherApi.banks(true, false).subscribe({
      next: (banks) => {
        const bankIds = banks.filter((b) => b.isActive && b.questionCount > 0).map((b) => b.id);
        this.liveApi
          .create({
            title: item.title,
            mode: 'Pedagogical',
            bankIds: bankIds.length ? bankIds : undefined,
            config: {
              caleStandardPreset: false,
              questionCount: 0,
              secondsPerQuestion: 30,
              randomize: true,
              shuffleOptions: true,
              showRanking: true,
              anonymousNames: false,
              feedbackTiming: 'immediate',
              bankIds: bankIds.length ? bankIds : undefined,
              presentationId: item.id
            }
          })
          .subscribe({
            next: (lobby) => {
              this.liveStarting.set(false);
              void this.router.navigate(['/teacher/live', lobby.sessionId, 'host'], {
                queryParams: { openDeck: 1 }
              });
            },
            error: (err) => {
              this.liveStarting.set(false);
              this.error.set(mapApiError(err));
            }
          });
      },
      error: (err) => {
        this.liveStarting.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  remove(item: PresentationListItem, ev: Event): void {
    ev.preventDefault();
    ev.stopPropagation();
    if (!confirm(`¿Eliminar "${item.title}"?`)) {
      return;
    }
    this.api.delete(item.id).subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  relative(iso: string): string {
    const t = new Date(iso).getTime();
    if (Number.isNaN(t)) {
      return '';
    }
    const diff = Date.now() - t;
    const m = Math.floor(diff / 60000);
    if (m < 1) {
      return 'hace un momento';
    }
    if (m < 60) {
      return `hace ${m} min`;
    }
    const h = Math.floor(m / 60);
    if (h < 24) {
      return `hace ${h} h`;
    }
    const d = Math.floor(h / 24);
    return `hace ${d} d`;
  }
}
