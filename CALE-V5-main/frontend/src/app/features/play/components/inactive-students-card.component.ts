import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { InactiveStudent, InactiveStudentsApi } from '../api/play.api';

const PREVIEW = 6;

@Component({
  selector: 'app-inactive-students-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, UiButtonComponent],
  template: `
    <section class="card" aria-labelledby="inactive-title">
      <header class="head">
        <div>
          <h2 id="inactive-title">Estudiantes sin practicar</h2>
          <p>Recordatorio para que no pierdan el ritmo.</p>
        </div>
        <label class="days">
          Sin actividad en
          <select [value]="days()" (change)="changeDays($event)">
            <option value="3">3 días</option>
            <option value="7">7 días</option>
            <option value="14">14 días</option>
            <option value="30">30 días</option>
          </select>
        </label>
      </header>

      @if (error()) {
        <p class="error" role="alert">{{ error() }}</p>
      }
      @if (message()) {
        <p class="ok" role="status">{{ message() }}</p>
      }

      @if (loading()) {
        <p class="muted">Cargando…</p>
      } @else if (!students().length) {
        <p class="muted">Todos han practicado en los últimos {{ days() }} días.</p>
      } @else {
        <p class="count"><strong>{{ students().length }}</strong> {{ students().length === 1 ? 'estudiante' : 'estudiantes' }} sin actividad</p>
        <ul class="list">
          @for (s of visible(); track s.userId) {
            <li>
              <span class="who">
                <strong>{{ s.name }}</strong>
                <small>
                  @if (s.lastActivityAt) {
                    {{ s.lastActivityAt | date: 'd MMM' }} · {{ s.daysInactive }} días
                  } @else {
                    Nunca ha practicado
                  }
                </small>
              </span>
              <button type="button" class="link" [disabled]="sending() || reminded().has(s.userId)" (click)="remind([s.userId])">
                {{ reminded().has(s.userId) ? 'Enviado' : 'Recordar' }}
              </button>
            </li>
          }
        </ul>
        <div class="actions">
          @if (students().length > preview) {
            <button type="button" class="link" (click)="expanded.set(!expanded())">
              {{ expanded() ? 'Ver menos' : 'Ver los ' + students().length }}
            </button>
          }
          <ui-button type="button" [loading]="sending()" [disabled]="pendingIds().length === 0" (click)="remind(pendingIds())">
            Recordar a todos ({{ pendingIds().length }})
          </ui-button>
        </div>
      }
    </section>
  `,
  styles: [`
    .card {
      display: grid;
      gap: 0.65rem;
      padding: 0.9rem 1rem;
      border-radius: 14px;
      border: 1px solid var(--color-border);
      border-left: 4px solid var(--color-warning);
      background: var(--color-surface);
      box-shadow: var(--shadow-sm);
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: 0.65rem;
      align-items: flex-start;
    }
    h2 {
      margin: 0;
      font-size: 0.98rem;
      font-weight: 750;
      letter-spacing: -0.02em;
    }
    .head p {
      margin: 0.15rem 0 0;
      color: var(--color-text-secondary);
      font-size: var(--text-sm);
      line-height: 1.35;
    }
    .days {
      display: grid;
      gap: 0.15rem;
      font-size: var(--text-xs);
      color: var(--color-text-secondary);
    }
    .days select {
      min-height: 2.25rem;
      padding: 0.3rem 0.5rem;
      font: inherit;
      font-size: var(--text-sm);
      color: var(--color-text);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-sm);
    }
    .count { margin: 0; font-size: var(--text-sm); }
    .list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0;
    }
    .list li {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.65rem;
      padding: 0.45rem 0;
      border-bottom: 1px solid color-mix(in srgb, var(--color-border) 70%, transparent);
      background: transparent;
    }
    .list li:last-child { border-bottom: 0; padding-bottom: 0; }
    .who { display: grid; gap: 0.05rem; min-width: 0; }
    .who strong {
      font-size: 0.92rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .who small { color: var(--color-text-secondary); font-size: var(--text-xs); }
    .link {
      border: 0;
      background: transparent;
      color: var(--color-primary);
      font: inherit;
      font-weight: 700;
      font-size: var(--text-sm);
      cursor: pointer;
      white-space: nowrap;
      min-height: 2.25rem;
      padding: 0 0.15rem;
    }
    .link:disabled { color: var(--color-text-secondary); cursor: default; }
    .actions {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      align-items: center;
      gap: 0.55rem;
      padding-top: 0.15rem;
    }
    .muted { margin: 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .error { margin: 0; color: var(--color-danger); font-size: var(--text-sm); }
    .ok { margin: 0; color: var(--color-success); font-weight: 600; font-size: var(--text-sm); }
  `]
})
export class InactiveStudentsCardComponent implements OnInit {
  private readonly api = inject(InactiveStudentsApi);

  readonly preview = PREVIEW;
  readonly days = signal(7);
  readonly loading = signal(true);
  readonly sending = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly students = signal<InactiveStudent[]>([]);
  readonly reminded = signal<Set<number>>(new Set());
  readonly expanded = signal(false);

  readonly visible = computed(() => (this.expanded() ? this.students() : this.students().slice(0, PREVIEW)));
  readonly pendingIds = computed(() =>
    this.students().map((s) => s.userId).filter((id) => !this.reminded().has(id))
  );

  ngOnInit(): void {
    this.load();
  }

  changeDays(event: Event): void {
    this.days.set(Number((event.target as HTMLSelectElement).value) || 7);
    this.load();
  }

  remind(ids: number[]): void {
    if (!ids.length) return;
    this.sending.set(true);
    this.error.set(null);
    this.message.set(null);
    this.api.remind(ids).subscribe({
      next: (res) => {
        this.sending.set(false);
        this.reminded.update((set) => new Set([...set, ...ids]));
        this.message.set(
          res.sent === 1 ? 'Recordatorio enviado.' : `Recordatorio enviado a ${res.sent} estudiantes.`
        );
      },
      error: (err) => {
        this.sending.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.message.set(null);
    this.api.list(this.days()).subscribe({
      next: (list) => {
        this.students.set(list);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }
}
