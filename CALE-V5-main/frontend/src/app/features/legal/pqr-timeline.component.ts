import { DatePipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { PQR_EVENT_LABEL, PQR_STATUS_LABEL, Pqr, PqrStatus } from './pqr.api';

/** Estado, plazo, respuesta e historial de una PQR, tal como lo ve quien la envió. */
@Component({
  selector: 'legal-pqr-timeline',
  standalone: true,
  imports: [DatePipe],
  template: `
    <div class="head">
      <strong>{{ pqr.radicado }}</strong>
      <span class="pill" [class.ok]="pqr.status === 'RESPONDIDA'" [class.bad]="pqr.status === 'RECHAZADA'">{{ label(pqr.status) }}</span>
    </div>
    <p class="subject">{{ pqr.subject }} <small>· {{ pqr.kindLabel }}</small></p>
    <p class="muted">
      Radicada el {{ pqr.createdAt | date: 'dd/MM/yyyy' }} ·
      @if (pqr.closedAt) {
        cerrada el {{ pqr.closedAt | date: 'dd/MM/yyyy' }}
      } @else {
        respuesta a más tardar el {{ pqr.dueAt | date: 'dd/MM/yyyy' }}@if (pqr.extended) { (plazo prorrogado) }
      }
    </p>
    @if (pqr.response) {
      <div class="response">
        <strong>Respuesta</strong>
        <p>{{ pqr.response }}</p>
      </div>
    }
    <details>
      <summary>Ver tu mensaje e historial</summary>
      <p class="message">{{ pqr.message }}</p>
      <ul class="events">
        @for (e of pqr.events; track $index) {
          <li>
            {{ e.createdAt | date: 'dd/MM/yyyy HH:mm' }} · {{ eventLabel(e.action) }}
            @if (e.toStatus && e.action === 'StatusChanged') { → {{ label(e.toStatus) }} }
            @if (e.action === 'Extended' && e.note) { <br /><small>{{ e.note }}</small> }
          </li>
        }
      </ul>
    </details>
  `,
  styles: [`
    :host { display: grid; gap: 0.45rem; }
    .head { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; justify-content: space-between; }
    p { margin: 0; line-height: 1.45; }
    .subject small, .muted { color: var(--color-text-secondary); font-size: 0.85rem; }
    .pill { font-size: 0.75rem; padding: 0.15rem 0.6rem; border-radius: 999px; border: 1px solid #c99400; }
    .pill.ok { border-color: var(--color-success); }
    .pill.bad { border-color: var(--color-danger); }
    .response { padding: 0.7rem 0.85rem; border-radius: var(--radius-md); background: var(--color-success-soft); }
    .response p { white-space: pre-line; margin-top: 0.25rem; }
    details { font-size: 0.88rem; }
    summary { cursor: pointer; color: var(--color-primary); font-weight: 600; }
    .message { white-space: pre-line; margin: 0.5rem 0; padding: 0.6rem 0.75rem; border-left: 3px solid var(--color-border); }
    .events { margin: 0; padding-left: 1.1rem; display: grid; gap: 0.25rem; }
  `]
})
export class PqrTimelineComponent {
  @Input({ required: true }) pqr!: Pqr;

  label(status: PqrStatus): string {
    return PQR_STATUS_LABEL[status] ?? status;
  }

  eventLabel(action: string): string {
    return PQR_EVENT_LABEL[action] ?? action;
  }
}
