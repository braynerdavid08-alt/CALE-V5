import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { TeacherApi, QuestionListDto } from '../../teacher/api/teacher.api';

@Component({
  selector: 'app-questions-page',
  standalone: true,
  imports: [
    RouterLink,
    FormsModule,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  template: `
    <ui-page-header
      [title]="canManage() ? 'Preguntas' : 'Catálogo de preguntas'"
      [subtitle]="canManage()
        ? 'Catálogo global de Mi CALE. Solo administración crea y edita preguntas.'
        : 'Solo lectura. Los instructores crean y asignan exámenes en su Biblioteca.'">
      @if (canManage()) {
        <a routerLink="/admin/questions/new"><ui-button type="button">Nueva pregunta</ui-button></a>
      }
    </ui-page-header>
    <ui-error [message]="error()" />
    <div class="search">
      <input
        class="input"
        [(ngModel)]="query"
        name="q"
        placeholder="Buscar por texto o banco" />
    </div>
    @if (loading()) {
      <ui-loading />
    } @else if (!visible().length) {
      <ui-empty
        title="No hay preguntas"
        [message]="canManage() ? 'Crea una o ajusta la búsqueda.' : 'Aún no hay catálogo publicado por administración.'" />
    } @else {
      <ui-card>
        <div class="table-wrap">
          <table class="data">
            <thead>
              <tr>
                <th>Enunciado</th>
                <th>Banco</th>
                <th>Tipo</th>
                <th>Estado</th>
              </tr>
            </thead>
            <tbody>
              @for (q of visible(); track q.id) {
                <tr>
                  <td>
                    @if (canManage()) {
                      <a [routerLink]="['/admin/questions', q.id]">{{ q.text }}</a>
                    } @else {
                      {{ q.text }}
                    }
                  </td>
                  <td>{{ q.bankName }}</td>
                  <td>{{ q.type }}</td>
                  <td>
                    <ui-badge [tone]="q.isActive ? 'success' : 'neutral'">
                      {{ q.isActive ? 'Activa' : 'Inactiva' }}
                    </ui-badge>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </ui-card>
    }
  `,
  styles: [`
    .search { margin-bottom: var(--spacing-md); }
  `]
})
export class QuestionsPage implements OnInit {
  private readonly api = inject(TeacherApi);
  private readonly session = inject(SessionStore);
  readonly items = signal<QuestionListDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly loading = signal(true);
  readonly canManage = computed(() => this.session.user()?.role === 'Admin');
  query = '';

  visible(): QuestionListDto[] {
    const q = this.query.trim().toLowerCase();
    const list = this.items();
    if (!q) {
      return list;
    }
    return list.filter((item) =>
      item.text.toLowerCase().includes(q)
      || item.bankName.toLowerCase().includes(q));
  }

  ngOnInit(): void {
    this.api.questions().subscribe({
      next: (page) => {
        this.items.set(page.items);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
