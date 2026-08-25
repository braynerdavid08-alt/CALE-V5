import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { GroupDto } from '../../student/api/student.api';
import { TeacherApi } from '../api/teacher.api';

@Component({
  selector: 'app-teacher-groups-page',
  standalone: true,
  imports: [
    RouterLink,
    FormsModule,
    UiButtonComponent,
    UiCardComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiPageHeaderComponent
  ],
  template: `
    <ui-page-header title="Grupos" subtitle="Aulas virtuales y códigos de invitación." />
    <ui-error [message]="error()" />
    <ui-card>
      <form class="row" (ngSubmit)="create()">
        <input class="input" [(ngModel)]="name" name="name" placeholder="Nombre del grupo" />
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
              </tr>
            </thead>
            <tbody>
              @for (group of groups(); track group.id) {
                <tr>
                  <td>
                    <a [routerLink]="['/teacher/groups', group.id]">{{ group.name }}</a>
                  </td>
                  <td>{{ group.code }}</td>
                  <td>{{ group.memberCount }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </ui-card>
    }
  `,
  styles: [`
    .input { flex: 1; min-width: 180px; }
  `]
})
export class TeacherGroupsPage implements OnInit {
  private readonly api = inject(TeacherApi);
  readonly groups = signal<GroupDto[]>([]);
  readonly error = signal<string | null>(null);
  name = '';

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
    this.api.createGroup(this.name.trim()).subscribe({
      next: () => {
        this.name = '';
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
