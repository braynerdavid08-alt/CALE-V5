import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { QuestionDraftEditorComponent } from '../components/question-draft-editor.component';
import {
  MyRequestStatus,
  QuestionDraft,
  RequestsApi,
  UserRequestDto,
  UserRequestKind,
  draftBody,
  emptyDraft,
  validateDraft
} from '../api/requests.api';

@Component({
  selector: 'app-my-requests-page',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    QuestionDraftEditorComponent,
    UiButtonComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiSuccessComponent
  ],
  styles: [`
    :host { display: block; }
    .wrap { width: min(980px, 100%); margin: 0 auto; display: grid; gap: 1.25rem; padding-bottom: 2rem; }
    .actions-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
    .action {
      display: grid;
      gap: 0.45rem;
      align-content: start;
      padding: 1.3rem;
      text-align: left;
      border-radius: 1.2rem;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
      cursor: pointer;
      transition: transform 0.15s ease, border-color 0.15s ease, box-shadow 0.15s ease;
    }
    .action:hover { transform: translateY(-2px); border-color: var(--color-primary); box-shadow: var(--shadow-sm); }
    .action .emoji { font-size: 2rem; line-height: 1; }
    .action strong { font-size: var(--text-lg); }
    .action span { color: var(--color-text-secondary); line-height: 1.45; }
    .action .cta { color: var(--color-primary); font-weight: 800; margin-top: 0.3rem; }
    .action.q { background: linear-gradient(135deg, color-mix(in srgb, var(--color-primary) 10%, var(--color-surface)), var(--color-surface)); }
    .action.i { background: linear-gradient(135deg, color-mix(in srgb, #f59e0b 12%, var(--color-surface)), var(--color-surface)); }
    .how { margin: 0; color: var(--color-text-secondary); font-size: var(--text-sm); line-height: 1.5; }
    h2 { margin: 0; font-size: var(--text-lg); }
    .list { display: grid; gap: 0.75rem; margin: 0; padding: 0; list-style: none; }
    .item {
      display: grid;
      gap: 0.4rem;
      padding: 1rem 1.1rem;
      border-radius: 1rem;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
    }
    .item-head { display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem; justify-content: space-between; }
    .item-title { font-weight: 800; overflow-wrap: anywhere; }
    .meta { color: var(--color-text-secondary); font-size: var(--text-sm); }
    .pill { padding: 0.2rem 0.65rem; border-radius: 999px; font-size: var(--text-xs); font-weight: 800; white-space: nowrap; }
    .pill.Pending { background: color-mix(in srgb, #f59e0b 18%, transparent); color: #b45309; }
    .pill.Accepted { background: var(--color-success-soft); color: var(--color-success); }
    .pill.Rejected { background: color-mix(in srgb, var(--color-danger, #dc2626) 14%, transparent); color: var(--color-danger, #dc2626); }
    .note { margin: 0; padding: 0.6rem 0.8rem; border-radius: 0.7rem; background: var(--color-surface-raised); font-size: var(--text-sm); line-height: 1.45; }
    .blocked-note { margin: 0 0 1rem; padding: 0.9rem 1rem; background: color-mix(in srgb, var(--color-danger, #dc2626) 10%, transparent); color: var(--color-danger, #b91c1c); overflow-wrap: anywhere; }
    .empty { text-align: center; padding: 1.5rem; color: var(--color-text-secondary); border: 1px dashed var(--color-border); border-radius: 1rem; }

    .overlay {
      position: fixed;
      inset: 0;
      z-index: var(--z-modal, 1000);
      background: var(--color-scrim, rgba(0, 0, 0, 0.5));
      display: grid;
      place-items: center;
      padding: 1rem;
    }
    .sheet {
      width: min(760px, 100%);
      max-height: calc(100dvh - 2rem);
      display: grid;
      grid-template-rows: auto 1fr auto;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 1.2rem;
      box-shadow: var(--shadow-md);
      overflow: hidden;
    }
    .sheet-head { display: flex; align-items: start; justify-content: space-between; gap: 1rem; padding: 1rem 1.2rem; border-bottom: 1px solid var(--color-border); }
    .sheet-head h2 { margin: 0; }
    .sheet-head p { margin: 0.25rem 0 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .close { border: 0; background: transparent; color: var(--color-text); font-size: 1.6rem; line-height: 1; cursor: pointer; padding: 0.2rem 0.4rem; }
    .sheet-body { overflow-y: auto; padding: 1.1rem 1.2rem; display: grid; gap: 1rem; }
    .sheet-foot { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 0.5rem; padding: 0.85rem 1.2rem; border-top: 1px solid var(--color-border); background: var(--color-surface); }
    textarea { min-height: 8rem; }
    @media (max-width: 700px) {
      .actions-grid { grid-template-columns: 1fr; }
      .overlay { padding: 0; place-items: stretch; }
      .sheet { width: 100%; max-height: 100dvh; height: 100dvh; border-radius: 0; border: 0; }
      .sheet-foot ui-button { flex: 1; }
    }
  `],
  template: `
    <section class="wrap">
      <ui-page-header
        title="Proponer y sugerir"
        subtitle="Envía preguntas CALE o ideas al administrador. Él revisa cada solicitud y decide si la agrega." />
      @if (ok()) { <ui-success [message]="ok()" /> }
      @if (error()) { <ui-error [message]="error()" /> }

      @if (blocked(); as b) {
        <p class="note blocked-note" role="status">
          <strong>El administrador desactivó el envío de solicitudes para tu cuenta.</strong>
          @if (b.reason) { <br />Motivo: {{ b.reason }} }
        </p>
      } @else {
      <div class="actions-grid">
        <button type="button" class="action q" (click)="open('question')">
          <span class="emoji" aria-hidden="true">📝</span>
          <strong>Pedir crear pregunta CALE</strong>
          <span>Escribe una pregunta con sus respuestas A, B, C, D o verdadero/falso, como lo hace un instructor.</span>
          <span class="cta">Crear pregunta →</span>
        </button>
        <button type="button" class="action i" (click)="open('idea')">
          <span class="emoji" aria-hidden="true">💡</span>
          <strong>Sugerir idea o modo nuevo</strong>
          <span>¿Un juego, un modo de práctica o una mejora que te gustaría? Cuéntanos.</span>
          <span class="cta">Escribir idea →</span>
        </button>
      </div>
      <p class="how">Recibirás una notificación cuando el administrador acepte o rechace tu solicitud.</p>
      }

      <h2>Mis solicitudes</h2>
      @if (loading()) {
        <ui-loading />
      } @else if (!items().length) {
        <p class="empty">Todavía no has enviado solicitudes.</p>
      } @else {
        <ul class="list">
          @for (r of items(); track r.id) {
            <li class="item">
              <div class="item-head">
                <span class="item-title">{{ r.kind === 'question' ? '📝' : '💡' }} {{ r.title }}</span>
                <span [class]="'pill ' + r.status">{{ statusLabel(r.status) }}</span>
              </div>
              <span class="meta">
                {{ r.kind === 'question' ? 'Pregunta propuesta' : 'Idea' }} · {{ r.createdAt | date:'d MMM y, h:mm a' }}
              </span>
              @if (r.adminNote) {
                <p class="note"><strong>Respuesta del administrador:</strong> {{ r.adminNote }}</p>
              }
              @if (r.status === 'Pending') {
                <div>
                  <ui-button type="button" variant="ghost" (click)="cancel(r)">Retirar solicitud</ui-button>
                </div>
              }
            </li>
          }
        </ul>
      }
    </section>

    @if (mode(); as m) {
      <div class="overlay" (click)="close()">
        <div class="sheet" role="dialog" aria-modal="true" aria-labelledby="req-title" (click)="$event.stopPropagation()">
          <header class="sheet-head">
            <div>
              <h2 id="req-title">{{ m === 'question' ? 'Pedir crear pregunta CALE' : 'Sugerir idea o modo nuevo' }}</h2>
              <p>{{ m === 'question'
                ? 'Completa la pregunta y marca la respuesta correcta. El administrador la revisará.'
                : 'Describe tu idea con el mayor detalle posible.' }}</p>
            </div>
            <button type="button" class="close" aria-label="Cerrar" (click)="close()">×</button>
          </header>
          <div class="sheet-body">
            @if (formError()) { <ui-error [message]="formError()" /> }
            @if (m === 'question') {
              <app-question-draft-editor [draft]="draft" />
              <label class="field">Mensaje para el administrador (opcional)
                <input [(ngModel)]="message" name="req-msg" maxlength="500" placeholder="Ej. La vi en un examen de práctica" />
              </label>
            } @else {
              <label class="field">Título de la idea
                <input [(ngModel)]="title" name="req-title" maxlength="200" placeholder="Ej. Modo contrarreloj por temas" />
              </label>
              <label class="field">Descripción
                <textarea [(ngModel)]="message" name="req-desc" maxlength="4000"
                  placeholder="¿Cómo funcionaría? ¿Por qué ayudaría a prepararse para el examen?"></textarea>
              </label>
            }
          </div>
          <footer class="sheet-foot">
            <ui-button type="button" variant="ghost" (click)="close()">Cancelar</ui-button>
            <ui-button type="button" [loading]="sending()" (click)="send()">Enviar al administrador</ui-button>
          </footer>
        </div>
      </div>
    }
  `
})
export class MyRequestsPage implements OnInit {
  private readonly api = inject(RequestsApi);

  readonly loading = signal(true);
  readonly items = signal<UserRequestDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly mode = signal<UserRequestKind | null>(null);
  readonly sending = signal(false);
  readonly formError = signal<string | null>(null);
  readonly blocked = signal<MyRequestStatus | null>(null);

  draft: QuestionDraft = emptyDraft();
  title = '';
  message = '';

  ngOnInit(): void {
    this.load();
    this.api.myStatus().subscribe({ next: (s) => this.blocked.set(s.blocked ? s : null) });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.mode() && !this.sending()) {
      this.close();
    }
  }

  open(kind: UserRequestKind): void {
    this.ok.set(null);
    this.formError.set(null);
    this.draft = emptyDraft();
    this.title = '';
    this.message = '';
    this.mode.set(kind);
  }

  close(): void {
    this.mode.set(null);
  }

  send(): void {
    const kind = this.mode();
    if (!kind) {
      return;
    }
    this.formError.set(null);
    let body: Parameters<RequestsApi['create']>[0];
    if (kind === 'question') {
      const problem = validateDraft(this.draft);
      if (problem) {
        this.formError.set(problem);
        return;
      }
      body = { kind, message: this.message.trim() || null, question: draftBody(this.draft) };
    } else {
      if (!this.title.trim() || !this.message.trim()) {
        this.formError.set('Escribe un título y describe tu idea.');
        return;
      }
      body = { kind, title: this.title.trim(), message: this.message.trim() };
    }
    this.sending.set(true);
    this.api.create(body).subscribe({
      next: (created) => {
        this.sending.set(false);
        this.mode.set(null);
        this.items.update((list) => [created, ...list]);
        this.ok.set('¡Enviado! El administrador revisará tu solicitud.');
      },
      error: (err) => {
        this.sending.set(false);
        this.formError.set(mapApiError(err));
      }
    });
  }

  cancel(r: UserRequestDto): void {
    this.api.cancel(r.id).subscribe({
      next: () => this.items.update((list) => list.filter((x) => x.id !== r.id)),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  statusLabel(status: string): string {
    if (status === 'Accepted') return 'Aceptada';
    if (status === 'Rejected') return 'No aceptada';
    return 'En revisión';
  }

  private load(): void {
    this.api.mine().subscribe({
      next: (rows) => {
        this.items.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }
}
