import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { TournamentApi, TournamentQuestionBank } from '../api/tournament.api';
import { PlayTopbarComponent } from '../components/play-topbar.component';

/** Value for <input type="datetime-local"> (local time, no seconds). */
function toLocalInput(date: Date): string {
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

const KIND_LABEL: Record<string, string> = {
  official: 'Oficial',
  exam: 'Examen',
  school: 'Escuela'
};

@Component({
  selector: 'app-tournament-create-page',
  standalone: true,
  imports: [FormsModule, UiButtonComponent, PlayTopbarComponent],
  styleUrls: ['./play-page.css', './tournament-page.css'],
  template: `
    <section class="play-page">
      <play-topbar
        title="Crear torneo"
        subtitle="Define las reglas. Los estudiantes se inscriben y las llaves se arman al iniciar."
        [back]="base"
        backLabel="Volver a torneos" />

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      <form class="play-card t-form" (ngSubmit)="submit()">
        <div class="t-field">
          <label for="t-name">Nombre del torneo</label>
          <input
            id="t-name"
            class="t-input"
            name="name"
            maxlength="120"
            autocomplete="off"
            placeholder="Copa Luz Verde"
            [(ngModel)]="name"
            required />
          <small>Entre 3 y 120 caracteres.</small>
        </div>

        <div class="t-field">
          <label for="t-desc">Descripción (opcional)</label>
          <textarea
            id="t-desc"
            class="t-input"
            name="description"
            rows="3"
            maxlength="500"
            placeholder="Reglas, premio o mensaje para los participantes"
            [(ngModel)]="description"></textarea>
        </div>

        <div class="t-row">
          <div class="t-field">
            <label for="t-date">Fecha y hora</label>
            <input id="t-date" class="t-input" type="datetime-local" name="scheduledAt" [(ngModel)]="scheduledAt" required />
          </div>
          <div class="t-field">
            <label for="t-cap">Cupos</label>
            <input
              id="t-cap"
              class="t-input"
              type="number"
              name="capacity"
              min="4"
              max="20"
              step="1"
              [(ngModel)]="capacity" />
            <small>Entre 4 y 20. El cuadro se arma con quienes se inscriban (BYE si hace falta).</small>
          </div>
        </div>

        <div class="t-row">
          <div class="t-field">
            <label for="t-secs">Segundos por pregunta</label>
            <input
              id="t-secs"
              class="t-input"
              type="number"
              name="secondsPerQuestion"
              min="10"
              max="120"
              step="5"
              [(ngModel)]="secondsPerQuestion" />
            <small>Entre 10 y 120.</small>
          </div>
        </div>

        <div class="t-field">
          <span class="t-label">Bancos de preguntas</span>
          <small>
            Elige uno o varios (oficiales, de escuela o de examen). Si no eliges ninguno, se usa la mezcla
            por defecto (oficial + escuela).
          </small>
          @if (banksLoading()) {
            <p class="t-banks-hint">Cargando bancos…</p>
          } @else if (banks().length === 0) {
            <p class="t-banks-hint">No hay bancos disponibles. Se usará la mezcla por defecto.</p>
          } @else {
            <div class="t-banks" role="group" aria-label="Bancos de preguntas">
              @for (b of banks(); track b.id) {
                <label class="t-bank">
                  <input type="checkbox" [checked]="isSelected(b.id)" (change)="toggleBank(b.id, $event)" />
                  <span>{{ b.name }}</span>
                  <em>{{ kindLabel(b.kind) }}</em>
                </label>
              }
            </div>
          }
        </div>

        <div class="play-actions">
          <ui-button [routerLink]="base" variant="ghost">Cancelar</ui-button>
          <ui-button type="submit" [loading]="busy()" [disabled]="!valid()">Crear torneo</ui-button>
        </div>
      </form>
    </section>
  `
})
export class TournamentCreatePage implements OnInit {
  private readonly api = inject(TournamentApi);
  private readonly router = inject(Router);

  readonly base: string = inject(ActivatedRoute).snapshot.data['base'] ?? '/teacher/tournaments';

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly banks = signal<TournamentQuestionBank[]>([]);
  readonly banksLoading = signal(true);
  readonly selectedBankIds = signal<number[]>([]);

  name = '';
  description = '';
  scheduledAt = toLocalInput(new Date(Date.now() + 60 * 60 * 1000));
  capacity = 20;
  secondsPerQuestion = 30;

  ngOnInit(): void {
    this.api.questionBanks().subscribe({
      next: (list) => {
        this.banks.set(list);
        this.banksLoading.set(false);
      },
      error: () => {
        this.banks.set([]);
        this.banksLoading.set(false);
      }
    });
  }

  kindLabel(kind: string): string {
    return KIND_LABEL[kind] ?? kind;
  }

  isSelected(id: number): boolean {
    return this.selectedBankIds().includes(id);
  }

  toggleBank(id: number, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const current = this.selectedBankIds();
    this.selectedBankIds.set(
      checked ? [...current, id] : current.filter((x) => x !== id)
    );
  }

  valid(): boolean {
    const name = this.name.trim();
    const seconds = Number(this.secondsPerQuestion);
    const capacity = Number(this.capacity);
    return (
      name.length >= 3 &&
      name.length <= 120 &&
      !!this.scheduledAt &&
      !Number.isNaN(new Date(this.scheduledAt).getTime()) &&
      Number.isFinite(capacity) &&
      capacity >= 4 &&
      capacity <= 20 &&
      Number.isFinite(seconds) &&
      seconds >= 10 &&
      seconds <= 120
    );
  }

  submit(): void {
    if (this.busy() || !this.valid()) return;
    this.busy.set(true);
    this.error.set(null);
    const bankIds = this.selectedBankIds();
    this.api
      .create({
        name: this.name.trim(),
        description: this.description.trim() || null,
        scheduledAt: new Date(this.scheduledAt).toISOString(),
        capacity: Math.round(Number(this.capacity)),
        secondsPerQuestion: Math.round(Number(this.secondsPerQuestion)),
        questionMode: 'random',
        bankIds: bankIds.length > 0 ? bankIds : null
      })
      .subscribe({
        next: (created) => {
          this.busy.set(false);
          void this.router.navigate([this.base, created.id]);
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(mapApiError(err));
        }
      });
  }
}
