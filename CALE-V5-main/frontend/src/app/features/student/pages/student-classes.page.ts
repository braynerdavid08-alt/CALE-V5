import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { SessionStore } from '../../../core/auth/session.store';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { itemStatusLabel } from '../../../shared/utils/item-status-label';
import { ActivityDto, GroupDto, StudentApi } from '../api/student.api';

@Component({
  selector: 'app-student-classes-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    UiButtonComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent
  ],
  template: `
    <ui-page-header
      eyebrow="Estudiante"
      title="Mis clases"
      subtitle="Elige el grupo que te indicó tu instructor y sigue tus actividades." />

    <ui-error [message]="error()" />

    @if (loading()) {
      <ui-loading />
    } @else {
      @if (!hasSchool()) {
        <section class="panel warn-panel">
          <h2>Necesitas una escuela</h2>
          <p class="lead">
            Solo los estudiantes vinculados a una escuela pueden ver y unirse a grupos.
            Ve a tu perfil y solicita unirte con el NIT o correo de tu CEA.
          </p>
          <ui-button routerLink="/profile" [queryParams]="{ tab: 'context' }" type="button">
            Ir a Perfil
          </ui-button>
        </section>
      }

      @if (hasSchool() && available().length) {
        <section class="panel">
          <h2>Grupos disponibles en tu escuela</h2>
          <p class="lead">Únete solo al grupo que te diga tu instructor.</p>
          <ul class="list">
            @for (g of available(); track g.id) {
              <li>
                <div class="row">
                  <div>
                    <strong>{{ g.name }}</strong>
                    <p class="meta">{{ g.teacherName || 'Sin instructor' }}</p>
                  </div>
                  <ui-button
                    type="button"
                    [loading]="joiningId() === g.id"
                    [disabled]="joiningId() === g.id"
                    (click)="joinById(g)">
                    Unirme
                  </ui-button>
                </div>
              </li>
            }
          </ul>
        </section>
      }

      <section class="panel">
        <h2>Mis grupos</h2>
        @if (!groups().length) {
          <ui-empty
            title="Aún no estás en un grupo"
            message="Cuando tu instructor te diga a cuál unirte, elígelo arriba." />
        } @else {
          <ul class="list">
            @for (g of groups(); track g.id) {
              <li>
                <a class="row" [routerLink]="['/student/group', g.id]">
                  <div>
                    <strong>{{ g.name }}</strong>
                    <p class="meta">{{ g.teacherName || 'Sin instructor' }}</p>
                  </div>
                  <span class="link">Abrir</span>
                </a>
              </li>
            }
          </ul>
        }
      </section>

      <section class="panel">
        <h2>Actividades pendientes</h2>
        @if (!pending().length) {
          <p class="empty">No hay actividades pendientes.</p>
        } @else {
          <ul class="list">
            @for (a of pending(); track a.id) {
              <li>
                <a class="row" [routerLink]="['/student/group', a.groupId]">
                  <div>
                    <strong>{{ a.title }}</strong>
                    <p class="meta">{{ statusLabel(a.status) }}</p>
                  </div>
                  <span class="link">Ver</span>
                </a>
              </li>
            }
          </ul>
        }
      </section>
    }
  `,
  styles: [`
    .panel {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 1.1rem 1.25rem;
      margin-bottom: 1rem;
    }
    .warn-panel {
      border-color: color-mix(in srgb, var(--color-warning) 45%, var(--color-border));
      background: color-mix(in srgb, var(--color-warning) 8%, var(--color-surface));
    }
    .panel h2 { margin: 0 0 0.5rem; font-size: 1.05rem; }
    .lead {
      margin: 0 0 0.85rem;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      line-height: 1.45;
      max-width: 36rem;
    }
    .list { list-style: none; margin: 0; padding: 0; }
    .list li + li { border-top: 1px solid var(--color-border); }
    .row {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: center;
      padding: 0.85rem 0;
      text-decoration: none;
      color: inherit;
    }
    .meta { margin: 0.2rem 0 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .link { color: var(--color-primary); font-weight: 600; font-size: var(--text-sm); }
    .empty { margin: 0; color: var(--color-text-secondary); }
  `]
})
export class StudentClassesPage implements OnInit {
  private readonly api = inject(StudentApi);
  private readonly session = inject(SessionStore);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly groups = signal<GroupDto[]>([]);
  readonly available = signal<GroupDto[]>([]);
  readonly pending = signal<ActivityDto[]>([]);
  readonly hasSchool = signal(false);
  readonly joiningId = signal<number | null>(null);
  readonly statusLabel = itemStatusLabel;

  ngOnInit(): void {
    this.session.refreshMe().subscribe({
      next: () => {
        this.hasSchool.set(!!this.session.user()?.schoolId);
        this.reload();
      },
      error: () => {
        this.hasSchool.set(!!this.session.user()?.schoolId);
        this.reload();
      }
    });
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.dashboard().subscribe({
      next: (d) => {
        this.groups.set(d.groups);
        this.available.set(d.availableGroups ?? []);
        this.pending.set(d.pendingActivities);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  joinById(group: GroupDto): void {
    if (!this.hasSchool() || this.joiningId() != null) {
      return;
    }
    this.joiningId.set(group.id);
    this.error.set(null);
    this.api.joinGroupById(group.id).subscribe({
      next: () => {
        this.joiningId.set(null);
        this.reload();
      },
      error: (err) => {
        this.joiningId.set(null);
        this.error.set(mapApiError(err));
      }
    });
  }
}
