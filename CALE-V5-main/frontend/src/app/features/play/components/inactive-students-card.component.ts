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
          <p>Envíales un recordatorio para que no pierdan el ritmo.</p>
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
        <p class="muted">🎉 Todos tus estudiantes han practicado en los últimos {{ days() }} días.</p>
      } @else {
        <p class="count"><strong>{{ students().length }}</strong> {{ students().length === 1 ? 'estudiante' : 'estudiantes' }} sin actividad</p>
        <ul class="list">
          @for (s of visible(); track s.userId) {
            <li>
              <span class="who">
                <strong>{{ s.name }}</strong>
                <small>
                  @if (s.lastActivityAt) {
                    Última actividad: {{ s.lastActivityAt | date: 'd MMM' }} ({{ s.daysInactive }} días)
                  } @else {
                    Nunca ha practicado
                  }
                </small>
              </span>
              <button type="button" class="link" [disabled]="sending() || reminded().has(s.userId)" (click)="remind([s.userId])">
                {{ reminded().has(s.userId) ? 'Enviado ✓' : 'Recordar' }}
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
      gap: 0.75rem;
      padding: 1.1rem 1.2rem;
      border-radius: var(--radius-lg);
      border: 1px solid var(--color-border);
      border-left: 4px solid #f59e0b;
      background: var(--color-surface);
    }
    .head { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 0.75rem; align-items: flex-start; }
    h2 { margin: 0; font-size: var(--text-lg); }
    .head p { margin: 0.2rem 0 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .days { display: grid; gap: 0.2rem; font-size: var(--text-xs); color: var(--color-text-secondary); }
    .days select {
      padding: 0.35rem 0.5rem;
      font: inherit;
      font-size: var(--text-sm);
      color: var(--color-text);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-sm);
    }
    .count { margin: 0; }
    .list { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.4rem; }
    .list li {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.75rem;
      padding: 0.55rem 0.75rem;
      border-radius: var(--radius-md);
      background: var(--color-surface-raised);
    }
    .who { display: grid; }
    .who small { color: var(--color-text-secondary); font-size: var(--text-xs); }
    .link {
      border: 0;
      background: transparent;
      color: var(--color-primary);
      font: inherit;
      font-weight: 700;
      cursor: pointer;
      white-space: nowrap;
    }
    .link:disabled { color: var(--color-text-secondary); cursor: default; }
    .actions { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 0.6rem; }
    .muted { margin: 0; color: var(--color-text-secondary); }
    .error { margin: 0; color: var(--color-danger); }
    .ok { margin: 0; color: var(--color-success); font-weight: 600; }
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
