import { Component, EventEmitter, Input, Output } from '@angular/core';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';

@Component({
  selector: 'app-game-show-rules',
  standalone: true,
  imports: [UiButtonComponent],
  template: `
    <section class="rules" [class.modal]="modal" role="dialog" aria-labelledby="gs-rules-title">
      <header>
        <p class="eyebrow">Antes de empezar</p>
        <h2 id="gs-rules-title">¿Cómo se juega 100 Estudiantes Dijeron?</h2>
        <p class="lead">
          Dos o más equipos compiten por adivinar las respuestas más populares del tablero.
          Cualquier compañero del equipo puede responder desde su celular.
        </p>
      </header>

      <ol class="steps">
        <li>
          <strong>1. Buzzer</strong>
          <span>Cuando diga RESPONDER, el primer equipo que pulse gana el enfrentamiento.</span>
        </li>
        <li>
          <strong>2. Enfrentamiento</strong>
          <span>Ese equipo da una respuesta. Si acierta, toma el control de la ronda. Si falla, pasa al siguiente equipo.</span>
        </li>
        <li>
          <strong>3. Control</strong>
          <span>El equipo con el control busca el resto del tablero. Cada fallo es un ✕ (hasta 3).</span>
        </li>
        <li>
          <strong>4. Robo</strong>
          <span>Con 3 ✕, los rivales pueden intentar robar el banco con una sola respuesta correcta.</span>
        </li>
        <li>
          <strong>5. Puntos</strong>
          <span>Las respuestas descubiertas suman al banco de la ronda; al ganarla, van al marcador del equipo.</span>
        </li>
      </ol>

      <ul class="tips">
        <li>Hablen en equipo, pero escriban claro (sinónimos también cuentan si el profesor los aceptó).</li>
        <li>Si repiten una respuesta ya descubierta, no pasa nada: no suma strike.</li>
        <li>El profesor puede forzar turnos, revelar respuestas y revisar intentos dudosos.</li>
      </ul>

      @if (showActions) {
        <div class="actions">
          @if (secondaryLabel) {
            <ui-button type="button" variant="ghost" (click)="secondary.emit()">{{ secondaryLabel }}</ui-button>
          }
          <ui-button type="button" (click)="primary.emit()">{{ primaryLabel }}</ui-button>
        </div>
      }
    </section>
  `,
  styles: `
    .rules {
      display: grid;
      gap: 0.9rem;
      padding: 1rem 1.1rem;
      border-radius: 16px;
      border: 1px solid var(--color-border);
      background: color-mix(in srgb, var(--color-primary) 6%, var(--color-surface));
      color: var(--color-text);
      text-align: left;
    }
    .rules.modal {
      max-width: 34rem;
      margin: 0 auto;
      box-shadow: 0 18px 40px rgba(0, 0, 0, 0.18);
      background: var(--color-surface);
    }
    .eyebrow {
      margin: 0;
      font-size: 0.72rem;
      font-weight: 800;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--color-primary);
    }
    h2 {
      margin: 0.2rem 0 0;
      font-size: clamp(1.15rem, 3vw, 1.45rem);
      line-height: 1.25;
    }
    .lead {
      margin: 0.35rem 0 0;
      color: var(--color-text-secondary);
      font-size: 0.95rem;
      line-height: 1.45;
    }
    .steps {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.55rem;
    }
    .steps li {
      display: grid;
      gap: 0.15rem;
      padding: 0.55rem 0.7rem;
      border-radius: 12px;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
    }
    .steps strong { font-size: 0.92rem; }
    .steps span {
      color: var(--color-text-secondary);
      font-size: 0.88rem;
      line-height: 1.4;
    }
    .tips {
      margin: 0;
      padding-left: 1.1rem;
      color: var(--color-text-secondary);
      font-size: 0.85rem;
      line-height: 1.45;
      display: grid;
      gap: 0.25rem;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.55rem;
      justify-content: flex-end;
      margin-top: 0.25rem;
    }
  `
})
export class GameShowRulesComponent {
  @Input() modal = false;
  @Input() showActions = true;
  @Input() primaryLabel = 'Entendido';
  @Input() secondaryLabel: string | null = null;
  @Output() readonly primary = new EventEmitter<void>();
  @Output() readonly secondary = new EventEmitter<void>();
}
