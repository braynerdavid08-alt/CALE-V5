import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, computed, inject, signal } from '@angular/core';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiSheetComponent } from '../../../shared/ui/ui-sheet.component';

export interface AccountStatusTarget {
  id: number;
  name: string;
  email: string;
  isActive: boolean;
}

interface AccountStatusEvent {
  id: number;
  action: 'Suspended' | 'Reactivated';
  reason: string;
  evidence: string | null;
  suspendedUntil: string | null;
  actorUserId: number | null;
  actorName: string | null;
  createdAt: string;
}

/** Suspensión y reactivación de cuentas con motivo, evidencia, duración e historial. */
@Component({
  selector: 'app-account-status-sheet',
  standalone: true,
  imports: [DatePipe, UiButtonComponent, UiErrorComponent, UiSheetComponent],
  template: `
    <ui-sheet
      [open]="!!user"
      [title]="user?.isActive ? 'Suspender cuenta' : 'Revisar suspensión'"
      (close)="closed.emit()">
      @if (user) {
        <p class="who"><strong>{{ user.name }}</strong> · {{ user.email }}</p>
        <ui-error [message]="error()" />

        @if (user.isActive) {
          <label class="field">
            <span>Motivo (lo verá el usuario al intentar entrar)</span>
            <textarea rows="3" maxlength="500" [value]="reason()" (input)="reason.set($any($event.target).value)"
              placeholder="Ej.: compartió preguntas del examen en redes sociales."></textarea>
            <small>{{ reason().trim().length }}/500 · mínimo 10 caracteres</small>
          </label>
          <label class="field">
            <span>Evidencia (solo para administradores)</span>
            <textarea rows="2" maxlength="1000" [value]="evidence()" (input)="evidence.set($any($event.target).value)"
              placeholder="Enlace, captura o descripción de lo ocurrido."></textarea>
          </label>
          <label class="field">
            <span>Duración</span>
            <select [value]="duration()" (change)="duration.set($any($event.target).value)">
              <option value="">Indefinida (hasta que un administrador la revise)</option>
              @for (d of durations; track d) {
                <option [value]="d">{{ d }} {{ d === 1 ? 'día' : 'días' }}</option>
              }
            </select>
          </label>
          <ui-button type="button" variant="danger" [disabled]="busy() || !reasonValid()" (click)="suspend()">
            Suspender cuenta
          </ui-button>
        } @else {
          <label class="field">
            <span>Resultado de la revisión (opcional)</span>
            <textarea rows="2" maxlength="500" [value]="reason()" (input)="reason.set($any($event.target).value)"
              placeholder="Ej.: revisado, el reporte no se confirmó."></textarea>
          </label>
          <ui-button type="button" [disabled]="busy()" (click)="reactivate()">Reactivar cuenta</ui-button>
        }

        <h3>Historial</h3>
        @if (loadingHistory()) {
          <p class="muted">Cargando…</p>
        } @else if (history().length === 0) {
          <p class="muted">Sin suspensiones registradas.</p>
        } @else {
          <ol class="history">
            @for (h of history(); track h.id) {
              <li [class.suspended]="h.action === 'Suspended'">
                <strong>{{ h.action === 'Suspended' ? 'Suspendida' : 'Reactivada' }}</strong>
                · {{ h.createdAt | date: 'dd/MM/yyyy HH:mm' }}
                · {{ h.actorName ?? (h.actorUserId ? 'Admin #' + h.actorUserId : 'Automático') }}
                <div>{{ h.reason }}</div>
                @if (h.action === 'Suspended') {
                  <div class="muted">
                    {{ h.suspendedUntil ? 'Hasta ' + (h.suspendedUntil | date: 'dd/MM/yyyy HH:mm') : 'Sin fecha de fin' }}
                  </div>
                }
                @if (h.evidence) {
                  <div class="muted">Evidencia: {{ h.evidence }}</div>
                }
              </li>
            }
          </ol>
        }
      }
    </ui-sheet>
  `,
  styles: [`
    .who { margin: 0 0 0.75rem; }
    .field { display: grid; gap: 0.3rem; margin-bottom: 0.75rem; }
    .field span { font-weight: 600; font-size: 0.9rem; }
    .field small { color: var(--color-text-secondary); font-size: 0.78rem; }
    textarea, select {
      width: 100%;
      padding: 0.5rem 0.65rem;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
    }
    h3 { margin: 1.25rem 0 0.5rem; font-size: 1rem; }
    .muted { color: var(--color-text-secondary); font-size: 0.85rem; }
    .history { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.6rem; }
    .history li {
      padding: 0.55rem 0.7rem;
      border: 1px solid var(--color-border);
      border-left: 4px solid var(--color-success, #2e7d32);
      border-radius: var(--radius-md);
      font-size: 0.9rem;
    }
    .history li.suspended { border-left-color: var(--color-danger, #c62828); }
  `]
})
export class AccountStatusSheetComponent implements OnChanges {
  private readonly http = inject(HttpClient);

  @Input() user: AccountStatusTarget | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() changed = new EventEmitter<AccountStatusTarget>();

  readonly durations = [1, 3, 7, 15, 30, 90];
  readonly reason = signal('');
  readonly evidence = signal('');
  readonly duration = signal('');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly history = signal<AccountStatusEvent[]>([]);
  readonly loadingHistory = signal(false);
  readonly reasonValid = computed(() => {
    const length = this.reason().trim().length;
    return length >= 10 && length <= 500;
  });

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['user'] || !this.user) {
      return;
    }
    this.reason.set('');
    this.evidence.set('');
    this.duration.set('');
    this.error.set(null);
    this.loadHistory(this.user.id);
  }

  suspend(): void {
    this.save({
      isActive: false,
      reason: this.reason().trim(),
      evidence: this.evidence().trim() || null,
      durationDays: this.duration() ? Number(this.duration()) : null
    });
  }

  reactivate(): void {
    this.save({ isActive: true, reason: this.reason().trim() || null });
  }

  private save(body: object): void {
    const user = this.user;
    if (!user) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.http.patch<AccountStatusTarget>(`${env.apiUrl}/api/admin/users/${user.id}/active`, body).subscribe({
      next: (updated) => {
        this.busy.set(false);
        this.changed.emit(updated);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private loadHistory(userId: number): void {
    this.loadingHistory.set(true);
    this.history.set([]);
    this.http.get<AccountStatusEvent[]>(`${env.apiUrl}/api/admin/users/${userId}/status-history`).subscribe({
      next: (rows) => {
        this.history.set(rows);
        this.loadingHistory.set(false);
      },
      error: (err) => {
        this.loadingHistory.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
