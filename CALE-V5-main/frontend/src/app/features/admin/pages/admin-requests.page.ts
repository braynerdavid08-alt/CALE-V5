import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { mapApiError } from '../../../core/http/map-api-error';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import { roleLabel } from '../../../shared/utils/role-label';
import { TeacherApi } from '../../teacher/api/teacher.api';
import { QuestionDraftEditorComponent } from '../../requests/components/question-draft-editor.component';
import {
  QuestionDraft,
  RequestsApi,
  UserRequestCounts,
  UserRequestDto,
  UserRequestStatus,
  draftBody,
  draftFromDto,
  validateDraft
} from '../../requests/api/requests.api';

type KindFilter = '' | 'question' | 'idea';

@Component({
  selector: 'app-admin-requests-page',
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
    .bar { display: flex; flex-wrap: wrap; gap: 0.5rem; align-items: center; justify-content: space-between; margin-bottom: 1rem; }
    .tabs { display: flex; flex-wrap: wrap; gap: 0.35rem; }
    .tabs button {
      padding: 0.5rem 0.9rem;
      border-radius: 999px;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }
    .tabs button.active { background: var(--color-primary-soft); color: var(--color-primary); border-color: transparent; }
    .count { margin-left: 0.3rem; padding: 0.05rem 0.45rem; border-radius: 999px; background: var(--color-surface-raised); font-size: var(--text-xs); }
    .bar > select {
      min-width: 10rem;
      min-height: 2.5rem;
      padding: 0.45rem 0.8rem;
      border-radius: 999px;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
      font-weight: 700;
    }
    .list { display: grid; gap: 0.9rem; margin: 0; padding: 0; list-style: none; }
    .card {
      display: grid;
      gap: 0.6rem;
      padding: 1.1rem 1.2rem;
      border-radius: 1rem;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
    }
    .head { display: flex; flex-wrap: wrap; gap: 0.5rem; justify-content: space-between; align-items: center; }
    .title { margin: 0; font-size: var(--text-base); font-weight: 800; overflow-wrap: anywhere; }
    .meta { color: var(--color-text-secondary); font-size: var(--text-sm); }
    .tag { padding: 0.2rem 0.6rem; border-radius: 999px; font-size: var(--text-xs); font-weight: 800; white-space: nowrap; }
    .tag.question { background: var(--color-primary-soft); color: var(--color-primary); }
    .tag.idea { background: color-mix(in srgb, #f59e0b 18%, transparent); color: #b45309; }
    .msg { margin: 0; white-space: pre-wrap; line-height: 1.5; overflow-wrap: anywhere; }
    .q-img { max-width: min(260px, 100%); border-radius: 0.6rem; border: 1px solid var(--color-border); }
    .opts { margin: 0; padding: 0; list-style: none; display: grid; gap: 0.35rem; }
    .opts li { display: flex; gap: 0.5rem; align-items: center; padding: 0.45rem 0.65rem; border-radius: 0.6rem; background: var(--color-surface-raised); overflow-wrap: anywhere; }
    .opts li.is-correct { background: var(--color-success-soft); color: var(--color-success); font-weight: 700; }
    .opts img { max-height: 48px; border-radius: 0.4rem; }
    .letter { font-weight: 800; min-width: 1.4rem; }
    .note { margin: 0; padding: 0.55rem 0.75rem; border-radius: 0.6rem; background: var(--color-surface-raised); font-size: var(--text-sm); }
    .row-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .reject-box { display: grid; gap: 0.5rem; padding: 0.75rem; border-radius: 0.75rem; border: 1px dashed var(--color-border); }
    .empty { text-align: center; padding: 1.5rem; color: var(--color-text-secondary); border: 1px dashed var(--color-border); border-radius: 1rem; }

    .overlay { position: fixed; inset: 0; z-index: var(--z-modal, 1000); background: var(--color-scrim, rgba(0,0,0,.5)); display: grid; place-items: center; padding: 1rem; }
    .sheet { width: min(800px, 100%); max-height: calc(100dvh - 2rem); display: grid; grid-template-rows: auto 1fr auto; background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 1.2rem; box-shadow: var(--shadow-md); overflow: hidden; }
    .sheet-head { display: flex; justify-content: space-between; gap: 1rem; align-items: start; padding: 1rem 1.2rem; border-bottom: 1px solid var(--color-border); }
    .sheet-head h2 { margin: 0; font-size: var(--text-lg); }
    .sheet-head p { margin: 0.25rem 0 0; color: var(--color-text-secondary); font-size: var(--text-sm); }
    .close { border: 0; background: transparent; color: var(--color-text); font-size: 1.6rem; cursor: pointer; line-height: 1; }
    .sheet-body { overflow-y: auto; padding: 1.1rem 1.2rem; display: grid; gap: 1rem; }
    .grid-2 { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.75rem; }
    .sheet-foot { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 0.5rem; padding: 0.85rem 1.2rem; border-top: 1px solid var(--color-border); }
    @media (max-width: 700px) {
      .grid-2 { grid-template-columns: 1fr; }
      .overlay { padding: 0; place-items: stretch; }
      .sheet { width: 100%; height: 100dvh; max-height: 100dvh; border-radius: 0; border: 0; }
      .sheet-foot ui-button { flex: 1; }
    }
  `],
  template: `
    <ui-page-header
      eyebrow="Contenido"
      title="Solicitudes de usuarios"
      subtitle="Preguntas CALE e ideas que envían los usuarios. Tú decides: aceptar (y agregar la pregunta a un banco) o rechazar." />
    @if (ok()) { <ui-success [message]="ok()" /> }
    @if (error()) { <ui-error [message]="error()" /> }

    <div class="bar">
      <div class="tabs" role="tablist">
        <button type="button" [class.active]="status() === 'Pending'" (click)="setStatus('Pending')">
          Pendientes<span class="count">{{ counts().pending }}</span>
        </button>
        <button type="button" [class.active]="status() === 'Accepted'" (click)="setStatus('Accepted')">
          Aceptadas<span class="count">{{ counts().accepted }}</span>
        </button>
        <button type="button" [class.active]="status() === 'Rejected'" (click)="setStatus('Rejected')">
          Rechazadas<span class="count">{{ counts().rejected }}</span>
        </button>
      </div>
      <select [ngModel]="kind()" (ngModelChange)="setKind($event)" aria-label="Tipo de solicitud">
        <option value="">Todas</option>
        <option value="question">Preguntas</option>
        <option value="idea">Ideas</option>
      </select>
    </div>

    @if (loading()) {
      <ui-loading />
    } @else if (!items().length) {
      <p class="empty">No hay solicitudes en esta lista.</p>
    } @else {
      <ul class="list">
        @for (r of items(); track r.id) {
          <li class="card">
            <div class="head">
              <h3 class="title">{{ r.question?.text || r.title }}</h3>
              <span class="tag" [class]="'tag ' + r.kind">{{ r.kind === 'question' ? 'Pregunta' : 'Idea' }}</span>
            </div>
            <span class="meta">{{ r.userName }} · {{ role(r.userRole) }} · {{ r.createdAt | date:'d MMM y, h:mm a' }}</span>

            @if (r.kind === 'question' && r.question; as q) {
              @if (q.imageUrl) {
                <img class="q-img" [src]="media(q.imageUrl)" alt="Imagen de la pregunta" />
              }
              <ul class="opts">
                @for (o of q.options; track $index) {
                  <li [class.is-correct]="o.isCorrect">
                    <span class="letter">{{ optLetter(q.type, $index) }}</span>
                    @if (o.imageUrl) { <img [src]="media(o.imageUrl)" alt="" /> }
                    <span>{{ o.text }}</span>
                    @if (o.isCorrect) { <span>✓</span> }
                  </li>
                }
              </ul>
              @if (q.explanation) {
                <p class="note"><strong>Explicación:</strong> {{ q.explanation }}</p>
              }
              @if (r.message) {
                <p class="note"><strong>Mensaje:</strong> {{ r.message }}</p>
              }
            } @else {
              <p class="msg">{{ r.message }}</p>
            }

            @if (r.adminNote) {
              <p class="note"><strong>Tu nota:</strong> {{ r.adminNote }}</p>
            }
            @if (r.createdQuestionId) {
              <span class="meta">Pregunta creada #{{ r.createdQuestionId }}</span>
            }

            @if (r.status === 'Pending') {
              @if (rejectingId() === r.id) {
                <div class="reject-box">
                  <label class="field">Motivo (se le envía al usuario, opcional)
                    <input [(ngModel)]="rejectNote" name="rejectNote" maxlength="1000" placeholder="Ej. Ya existe una pregunta igual" />
                  </label>
                  <div class="row-actions">
                    <ui-button type="button" variant="danger" [loading]="busyId() === r.id" (click)="reject(r)">Confirmar rechazo</ui-button>
                    <ui-button type="button" variant="ghost" (click)="rejectingId.set(null)">Cancelar</ui-button>
                  </div>
                </div>
              } @else {
                <div class="row-actions">
                  @if (r.kind === 'question') {
                    <ui-button type="button" (click)="openReview(r)">Revisar y aceptar</ui-button>
                  } @else {
                    <ui-button type="button" [loading]="busyId() === r.id" (click)="acceptIdea(r)">Aceptar idea</ui-button>
                  }
                  <ui-button type="button" variant="ghost" (click)="startReject(r)">Rechazar</ui-button>
                </div>
              }
            }
          </li>
        }
      </ul>
    }

    @if (reviewing(); as r) {
      <div class="overlay" (click)="closeReview()">
        <div class="sheet" role="dialog" aria-modal="true" aria-labelledby="rev-title" (click)="$event.stopPropagation()">
          <header class="sheet-head">
            <div>
              <h2 id="rev-title">Revisar pregunta de {{ r.userName }}</h2>
              <p>Corrige lo que haga falta, elige banco y bloque, y acéptala.</p>
            </div>
            <button type="button" class="close" aria-label="Cerrar" (click)="closeReview()">×</button>
          </header>
          <div class="sheet-body">
            @if (reviewError()) { <ui-error [message]="reviewError()" /> }
            <div class="grid-2">
              <label class="field">Banco
                <select [(ngModel)]="bankId" name="bankId">
                  <option [ngValue]="0">Selecciona un banco</option>
                  @for (b of banks(); track b.id) {
                    <option [ngValue]="b.id">{{ b.name }}</option>
                  }
                </select>
              </label>
              <label class="field">Bloque / tema de contenido
                <select [(ngModel)]="blockId" name="blockId">
                  @for (b of blocks(); track b.id) {
                    <option [ngValue]="b.id">{{ b.name }}</option>
                  }
                </select>
              </label>
            </div>
            <app-question-draft-editor [draft]="draft" />
            <label class="field">Nota para el usuario (opcional)
              <input [(ngModel)]="acceptNote" name="acceptNote" maxlength="1000" placeholder="Ej. ¡Gracias! Ajusté la redacción." />
            </label>
          </div>
          <footer class="sheet-foot">
            <ui-button type="button" variant="ghost" (click)="closeReview()">Cancelar</ui-button>
            <ui-button type="button" [loading]="busyId() === r.id" (click)="acceptQuestion(r)">Aceptar y agregar al banco</ui-button>
          </footer>
        </div>
      </div>
    }
  `
})
export class AdminRequestsPage implements OnInit {
  private readonly api = inject(RequestsApi);
  private readonly teacherApi = inject(TeacherApi);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly ok = signal<string | null>(null);
  readonly items = signal<UserRequestDto[]>([]);
  readonly counts = signal<UserRequestCounts>({ pending: 0, accepted: 0, rejected: 0 });
  readonly status = signal<UserRequestStatus>('Pending');
  readonly kind = signal<KindFilter>('');
  readonly busyId = signal<number | null>(null);
  readonly rejectingId = signal<number | null>(null);
  readonly reviewing = signal<UserRequestDto | null>(null);
  readonly reviewError = signal<string | null>(null);
  readonly banks = signal<Array<{ id: number; name: string }>>([]);
  readonly blocks = signal<Array<{ id: number; name: string }>>([]);
  readonly media = resolveMediaUrl;

  rejectNote = '';
  acceptNote = '';
  bankId = 0;
  blockId = 0;
  draft!: QuestionDraft;

  ngOnInit(): void {
    this.reload();
    this.teacherApi.banks(false).subscribe({ next: (b) => this.banks.set(b) });
    this.teacherApi.blocks().subscribe({
      next: (b) => {
        this.blocks.set(b);
        if (!this.blockId && b[0]) this.blockId = b[0].id;
      }
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.reviewing() && this.busyId() === null) this.closeReview();
  }

  role(r: string): string {
    return roleLabel(r);
  }

  optLetter(type: string, i: number): string {
    return type === 'Verdadero/Falso' ? (i === 0 ? 'V' : 'F') : String.fromCharCode(65 + i);
  }

  setStatus(s: UserRequestStatus): void {
    this.status.set(s);
    this.reload();
  }

  setKind(k: KindFilter): void {
    this.kind.set(k);
    this.reload();
  }

  startReject(r: UserRequestDto): void {
    this.rejectNote = '';
    this.rejectingId.set(r.id);
  }

  reject(r: UserRequestDto): void {
    this.busyId.set(r.id);
    this.api.reject(r.id, this.rejectNote.trim() || null).subscribe({
      next: () => this.done(r, 'Solicitud rechazada. Se le avisó al usuario.'),
      error: (err) => this.fail(err)
    });
  }

  acceptIdea(r: UserRequestDto): void {
    this.busyId.set(r.id);
    this.api.accept(r.id, { note: null }).subscribe({
      next: () => this.done(r, 'Idea aceptada. Se le avisó al usuario.'),
      error: (err) => this.fail(err)
    });
  }

  openReview(r: UserRequestDto): void {
    if (!r.question) return;
    this.draft = draftFromDto(r.question);
    this.acceptNote = '';
    this.reviewError.set(null);
    const official = this.banks().find((b) => /normas/i.test(b.name) || /señal|senal/i.test(b.name));
    if (!this.bankId) this.bankId = official?.id ?? this.banks()[0]?.id ?? 0;
    this.reviewing.set(r);
  }

  closeReview(): void {
    this.reviewing.set(null);
  }

  acceptQuestion(r: UserRequestDto): void {
    this.reviewError.set(null);
    if (!this.bankId || !this.blockId) {
      this.reviewError.set('Elige el banco y el bloque.');
      return;
    }
    const problem = validateDraft(this.draft);
    if (problem) {
      this.reviewError.set(problem);
      return;
    }
    this.busyId.set(r.id);
    this.api.accept(r.id, {
      note: this.acceptNote.trim() || null,
      bankId: this.bankId,
      blockId: this.blockId,
      question: draftBody(this.draft)
    }).subscribe({
      next: (res) => {
        this.reviewing.set(null);
        this.done(r, `Pregunta #${res.createdQuestionId} agregada al banco. Se le avisó al usuario.`);
      },
      error: (err) => {
        this.busyId.set(null);
        this.reviewError.set(mapApiError(err));
      }
    });
  }

  private done(r: UserRequestDto, message: string): void {
    this.busyId.set(null);
    this.rejectingId.set(null);
    this.items.update((list) => list.filter((x) => x.id !== r.id));
    this.ok.set(message);
    this.loadCounts();
  }

  private fail(err: unknown): void {
    this.busyId.set(null);
    this.error.set(mapApiError(err));
  }

  private reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.adminList(this.status(), this.kind() || undefined).subscribe({
      next: (rows) => {
        this.items.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
    this.loadCounts();
  }

  private loadCounts(): void {
    this.api.adminCounts().subscribe({ next: (c) => this.counts.set(c) });
  }
}
