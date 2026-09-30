import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SessionStore } from '../../../core/auth/session.store';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { BankAdminDto, BankUsageDto, TeacherApi } from '../../teacher/api/teacher.api';

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
    <ui-page-header
      [title]="canManage() ? 'Bancos' : 'Bancos del catálogo'"
      [subtitle]="canManage()
        ? 'Organiza las preguntas por banco (solo administración).'
        : 'Solo lectura. Los instructores crean y asignan exámenes en su Biblioteca.'" />
    <ui-error [message]="error()" />
    <ui-success [message]="ok()" />
    @if (canManage()) {
      <ui-card>
        <form class="row" (ngSubmit)="create()">
          <input class="input" [(ngModel)]="name" name="name" placeholder="Nombre del banco" />
          <input class="input" [(ngModel)]="description" name="desc" placeholder="Descripción" />
          <ui-button type="submit">Crear</ui-button>
        </form>
      </ui-card>
    }
    @if (!items().length) {
      <ui-empty title="No hay bancos" message="Administración aún no ha publicado bancos." />
    } @else {
      <ui-card>
        <div class="table-wrap">
          <table class="data">
            <thead>
              <tr>
                <th>Nombre</th>
                <th>Preguntas</th>
                <th>Estado</th>
                @if (canManage()) {
                  <th>Uso real</th>
                  <th></th>
                }
              </tr>
            </thead>
            <tbody>
              @for (bank of sortedItems(); track bank.id) {
                <tr [class.is-duplicate]="usageOf(bank.id)?.duplicateRole === 'duplicate'">
                  <td data-label="Nombre">
                    <strong>{{ bank.name }}</strong>
                    @if (usageOf(bank.id); as u) {
                      <span class="sub">#{{ bank.id }} · creado {{ shortDate(u.createdAt) }}</span>
                    }
                  </td>
                  <td data-label="Preguntas">{{ bank.questionCount }}</td>
                  <td data-label="Estado">
                    <ui-badge [tone]="bank.isActive ? 'success' : 'neutral'">
                      {{ bank.isActive ? 'Activo' : 'Inactivo' }}
                    </ui-badge>
                  </td>
                  @if (canManage()) {
                    <td data-label="Uso real" class="usage">
                      @if (usageOf(bank.id); as u) {
                        <div class="usage-tags">
                          @if (u.duplicateRole === 'main') {
                            <ui-badge tone="success">Principal (el que se usa)</ui-badge>
                          } @else if (u.duplicateRole === 'duplicate') {
                            <ui-badge tone="warning">Duplicado</ui-badge>
                          }
                          <ui-badge [tone]="u.inUse ? 'primary' : 'neutral'">
                            {{ u.inUse ? 'En uso' : 'Sin uso' }}
                          </ui-badge>
                        </div>
                        <span class="sub">
                          {{ u.publishedExams }} de {{ u.exams }} exámenes publicados ·
                          {{ u.attempts }} intentos ({{ u.attemptsLast30Days }} en 30 días) ·
                          {{ u.students }} estudiantes
                          @if (u.liveSessions) { · {{ u.liveSessions }} clases en vivo }
                        </span>
                        <span class="sub">
                          Último uso: {{ u.lastUsedAt ? shortDate(u.lastUsedAt) : 'nunca' }}
                        </span>
                        @if (u.schools.length) {
                          <span class="sub">Escuelas: {{ u.schools.join(', ') }}</span>
                        }
                      } @else {
                        <span class="sub">Cargando…</span>
                      }
                    </td>
                    <td class="actions">
                      <ui-button type="button" variant="ghost" (click)="toggle(bank)">
                        {{ bank.isActive ? 'Desactivar' : 'Activar' }}
                      </ui-button>
                      <ui-button type="button" variant="ghost" (click)="remove(bank)">
                        Borrar
                      </ui-button>
                    </td>
                  }
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
    .actions { display: flex; flex-wrap: wrap; gap: 0.35rem; }
    .sub { display: block; margin-top: 0.2rem; font-size: 0.8rem; color: var(--color-text-secondary); }
    .usage { min-width: 16rem; }
    @media (max-width: 700px) { .usage { min-width: 0; } }
    .usage-tags { display: flex; flex-wrap: wrap; gap: 0.3rem; }
    tr.is-duplicate td { opacity: 0.75; }
  `]
})
export class AdminBanksPage implements OnInit {
  private readonly api = inject(TeacherApi);
  private readonly session = inject(SessionStore);
  readonly items = signal<BankAdminDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly canManage = computed(() => this.session.user()?.role === 'Admin');
  readonly usage = signal<Map<number, BankUsageDto>>(new Map());
  readonly sortedItems = computed(() => {
    const usage = this.usage();
    const rank = (id: number) => {
      const role = usage.get(id)?.duplicateRole;
      return role === 'main' ? 0 : role === 'duplicate' ? 2 : 1;
    };
    return [...this.items()].sort((a, b) =>
      a.name.trim().localeCompare(b.name.trim(), 'es', { sensitivity: 'base' })
      || rank(a.id) - rank(b.id));
  });
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
    if (this.canManage()) {
      this.api.bankUsage().subscribe({
        next: (rows) => this.usage.set(new Map(rows.map((r) => [r.bankId, r]))),
        error: () => this.usage.set(new Map())
      });
    }
  }

  usageOf(bankId: number): BankUsageDto | undefined {
    return this.usage().get(bankId);
  }

  shortDate(iso: string): string {
    return new Date(iso).toLocaleDateString('es-CO', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  create(): void {
    if (!this.canManage() || !this.name.trim()) {
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
    if (!this.canManage()) {
      return;
    }
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

  remove(bank: BankAdminDto): void {
    if (!this.canManage()) {
      return;
    }
    const label = bank.name?.trim() || `banco #${bank.id}`;
    const u = this.usageOf(bank.id);
    const inUseWarning = u?.inUse
      ? `⚠️ ESTE BANCO ESTÁ EN USO: ${u.students} estudiantes, ${u.publishedExams} exámenes publicados`
        + (u.schools.length ? `, escuelas: ${u.schools.join(', ')}` : '') + '.\n\n'
      : '';
    if (!confirm(
      inUseWarning
      + `¿Borrar permanentemente «${label}» (#${bank.id})?\n\n`
      + 'Se eliminará de todos lados: sus preguntas, los exámenes que lo usan, los intentos y resultados '
      + 'de los estudiantes en esos exámenes y las clases en vivo que lo usaron.\n\n'
      + 'Esta acción no se puede deshacer.'
    )) {
      return;
    }
    this.error.set(null);
    this.api.deleteBank(bank.id).subscribe({
      next: () => {
        this.ok.set('Banco borrado.');
        this.reload();
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }
}
