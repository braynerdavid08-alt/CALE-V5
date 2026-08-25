import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { GroupDto } from '../../student/api/student.api';
import { TeacherApi } from '../api/teacher.api';

@Component({
  selector: 'app-teacher-groups-page',
  standalone: true,
  imports: [
    RouterLink,
    FormsModule,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiPageHeaderComponent,
    UiSuccessComponent
  ],
  template: `
    <ui-page-header title="Grupos" subtitle="Aulas virtuales y códigos de invitación." />
    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />
    <ui-card>
      <form class="stack" (ngSubmit)="create()">
        <label class="field">Nombre
          <input class="input" [(ngModel)]="name" name="name" placeholder="Nombre del grupo" />
        </label>
        <label class="field">Descripción
          <input class="input" [(ngModel)]="description" name="description" />
        </label>
        <ui-button type="submit">Crear</ui-button>
      </form>
    </ui-card>
    @if (!groups().length) {
      <ui-empty title="No hay grupos" message="Crea uno para invitar estudiantes." />
    } @else {
      <ui-card>
        <div class="table-wrap">
          <table class="data">
            <thead>
              <tr>
                <th>Nombre</th>
                <th>Código</th>
                <th>Integrantes</th>
                <th>Estado</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (group of groups(); track group.id) {
                <tr>
                  <td>
                    <a [routerLink]="['/teacher/groups', group.id]">{{ group.name }}</a>
                    @if (group.description) {
                      <p class="muted">{{ group.description }}</p>
                    }
                  </td>
                  <td>{{ group.code }}</td>
                  <td>{{ group.memberCount }}</td>
                  <td>
                    <ui-badge [tone]="group.isActive ? 'success' : 'danger'">
                      {{ group.isActive ? 'Activo' : 'Archivado' }}
                    </ui-badge>
                  </td>
                  <td>
                    <ui-button type="button" variant="ghost" (click)="toggleArchive(group)">
                      {{ group.isActive ? 'Archivar' : 'Reactivar' }}
                    </ui-button>
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
    .input { width: 100%; }
  `]
})
export class TeacherGroupsPage implements OnInit {
  private readonly api = inject(TeacherApi);
  readonly groups = signal<GroupDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  name = '';
  description = '';

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.api.groups().subscribe({
      next: (items) => this.groups.set(items),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  create(): void {
    if (!this.name.trim()) {
      return;
    }
    this.api.createGroup(this.name.trim(), this.description.trim() || undefined).subscribe({
      next: () => {
        this.name = '';
        this.description = '';
        this.ok.set('Grupo creado.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  toggleArchive(group: GroupDto): void {
    this.api.updateGroup(group.id, {
      name: group.name,
      description: group.description,
      isActive: !group.isActive
    }).subscribe({
      next: () => {
        this.ok.set(group.isActive ? 'Grupo archivado.' : 'Grupo reactivado.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
