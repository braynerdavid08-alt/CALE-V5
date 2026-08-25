import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { BankAdminDto, TeacherApi } from '../../teacher/api/teacher.api';

@Component({
  selector: 'app-admin-banks-page',
  standalone: true,
  imports: [
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
    <ui-page-header title="Bancos" subtitle="Organiza las preguntas por banco." />
    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />
    <ui-card>
      <form class="row" (ngSubmit)="create()">
        <input class="input" [(ngModel)]="name" name="name" placeholder="Nombre del banco" />
        <input class="input" [(ngModel)]="description" name="desc" placeholder="Descripción" />
        <ui-button type="submit">Crear</ui-button>
      </form>
    </ui-card>
    @if (!items().length) {
      <ui-empty title="No hay bancos" message="Crea el primero para empezar." />
    } @else {
      <ui-card>
        <div class="table-wrap">
          <table class="data">
            <thead>
              <tr>
                <th>Nombre</th>
                <th>Preguntas</th>
                <th>Estado</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (bank of items(); track bank.id) {
                <tr>
                  <td>{{ bank.name }}</td>
                  <td>{{ bank.questionCount }}</td>
                  <td>
                    <ui-badge [tone]="bank.isActive ? 'success' : 'neutral'">
                      {{ bank.isActive ? 'Activo' : 'Inactivo' }}
                    </ui-badge>
                  </td>
                  <td>
                    <ui-button type="button" variant="ghost" (click)="toggle(bank)">
                      {{ bank.isActive ? 'Desactivar' : 'Activar' }}
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
    .input { max-width: 240px; }
  `]
})
export class AdminBanksPage implements OnInit {
  private readonly api = inject(TeacherApi);
  readonly items = signal<BankAdminDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  name = '';
  description = '';

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.api.banks(false).subscribe({
      next: (items) => this.items.set(items),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  create(): void {
    if (!this.name.trim()) {
      return;
    }
    this.api.createBank(this.name.trim(), this.description.trim() || undefined)
      .subscribe({
        next: () => {
          this.name = '';
          this.description = '';
          this.ok.set('Banco creado.');
          this.reload();
        },
        error: (err) => this.error.set(mapApiError(err))
      });
  }

  toggle(bank: BankAdminDto): void {
    this.api.updateBank(
      bank.id,
      bank.name,
      bank.description ?? null,
      !bank.isActive
    ).subscribe({
      next: () => {
        this.ok.set('Banco actualizado.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
