import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { PresentationApi } from './presentation.api';
import {
  PRESENTATION_CATEGORIES,
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
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<PresentationListItem[]>([]);
  readonly creating = signal(false);
  readonly showCreate = signal(false);

  title = '';
  description = '';
  category: string = PRESENTATION_CATEGORIES[0];
  templateKey = 'cover';
  readonly categories = PRESENTATION_CATEGORIES;
  readonly templates = TEMPLATE_OPTIONS;

  ngOnInit(): void {
    this.reload();
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

  openCreate(): void {
    this.showCreate.set(true);
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

  duplicate(item: PresentationListItem, ev: Event): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.api.duplicate(item.id).subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(mapApiError(err))
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
