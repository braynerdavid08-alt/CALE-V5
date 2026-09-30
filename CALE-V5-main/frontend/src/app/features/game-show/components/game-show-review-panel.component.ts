import { Component, OnDestroy, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  GameShowApi,
  GameShowHostReviewDto,
  GameShowLobbyDto,
  GameShowReviewAnswerDto,
  GameShowReviewAttemptDto,
  GameShowReviewRoundDto
} from '../api/game-show.api';

type PickerKind = 'accept' | 'alias';

@Component({
  selector: 'app-game-show-review-panel',
  standalone: true,
  template: `
    <article class="review">
      <header class="head">
        <div>
          <h3>Respuestas de los estudiantes</h3>
          <p class="sub">Revisa lo que escribieron. Si el juego se equivocó, corrígelo o guarda la respuesta como sinónimo.</p>
        </div>
        @if (data(); as D) {
          @if (D.rounds.length > 1) {
            <label class="round-pick">
              Ronda
              <select [value]="selectedRoundId() ?? ''" (change)="pickRound($any($event.target).value)">
                @for (r of D.rounds; track r.roundId) {
                  <option [value]="r.roundId">{{ r.sortOrder + 1 }}{{ r.isCurrent ? ' (actual)' : '' }}</option>
                }
              </select>
            </label>
          }
        }
      </header>

      @if (error()) { <p class="msg err">{{ error() }}</p> }
      @if (notice()) { <p class="msg ok">{{ notice() }}</p> }

      @if (round(); as R) {
        <p class="q">{{ R.questionText }}</p>
        <p class="save-hint">Los sinónimos se guardan en: <strong>{{ aliasTarget(R) }}</strong></p>

        @if (!R.attempts.length) {
          <p class="empty">Todavía nadie ha respondido en esta ronda.</p>
        }

        <ul class="list">
          @for (a of R.attempts; track a.id) {
            <li [class.good]="a.isCorrect" [class.bad]="!a.isCorrect">
              <div class="row">
                <span class="who">
                  <strong>{{ a.playerName || 'Jugador' }}</strong>
                  <span class="team">{{ teamName(a.team) }}{{ a.isSteal ? ' · robo' : '' }}</span>
                </span>
                <span class="verdict">
                  @if (a.isCorrect) {
                    ✅ {{ answerLabel(R, a.matchedAnswerId) }}
                  } @else {
                    ❌ Incorrecta
                  }
                </span>
              </div>
              <p class="said">«{{ a.text }}»</p>
              <div class="actions">
                @if (a.acceptMode) {
                  <button type="button" class="btn primary" [disabled]="busy()" (click)="openPicker(a, 'accept')">Aceptar como correcta</button>
                }
                @if (a.canReject) {
                  <button type="button" class="btn danger" [disabled]="busy()" (click)="reject(a)">Marcar incorrecta</button>
                }
                <button type="button" class="btn" [disabled]="busy()" (click)="openPicker(a, 'alias')">Agregar como sinónimo</button>
              </div>

              @if (picker()?.attemptId === a.id) {
                <div class="picker">
                  <p class="pick-title">
                    {{ picker()?.kind === 'accept' ? '¿A qué respuesta del tablero corresponde?' : '¿De qué respuesta es sinónimo «' + a.text + '»?' }}
                  </p>
                  <div class="chips">
                    @for (ans of pickerAnswers(R, a); track ans.id) {
                      <button type="button" class="chip" [disabled]="busy()" (click)="choose(R, a, ans)">
                        <span class="rank">{{ ans.rank }}</span>
                        {{ hideAnswers() && !ans.isRevealed ? 'Respuesta ' + ans.rank : ans.text }}
                        <span class="pts">{{ ans.points }}</span>
                      </button>
                    }
                  </div>
                  @if (picker()?.kind === 'accept') {
                    <label class="also">
                      <input type="checkbox" [checked]="alsoAlias()" (change)="alsoAlias.set($any($event.target).checked)" />
                      Guardar también «{{ a.text }}» como sinónimo
                    </label>
                  }
                  <button type="button" class="btn ghost" (click)="picker.set(null)">Cancelar</button>
                </div>
              }
            </li>
          }
        </ul>
      } @else if (!error()) {
        <p class="empty">Cargando respuestas…</p>
      }
    </article>
  `,
  styles: [`
    .review { background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 16px; padding: 1rem; display: grid; gap: 0.6rem; min-width: 0; }
    .head { display: flex; justify-content: space-between; gap: 0.75rem; flex-wrap: wrap; align-items: flex-start; }
    h3 { margin: 0; }
    .sub, .save-hint, .empty { margin: 0.2rem 0 0; color: var(--color-text-secondary); font-size: 0.9rem; }
    .round-pick { display: flex; gap: 0.4rem; align-items: center; font-weight: 700; font-size: 0.9rem; }
    .round-pick select { padding: 0.35rem 0.5rem; border-radius: 8px; border: 1px solid var(--color-border); background: var(--color-surface); color: inherit; }
    .q { margin: 0; font-weight: 700; }
    .msg { margin: 0; font-weight: 700; }
    .msg.err { color: var(--color-danger, #dc2626); }
    .msg.ok { color: var(--color-success, #16a34a); }
    .list { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.55rem; }
    .list > li { border: 1px solid var(--color-border); border-left-width: 4px; border-radius: 12px; padding: 0.6rem 0.75rem; display: grid; gap: 0.35rem; min-width: 0; }
    .list > li.good { border-left-color: var(--color-success, #16a34a); }
    .list > li.bad { border-left-color: var(--color-danger, #dc2626); }
    .row { display: flex; justify-content: space-between; gap: 0.5rem; flex-wrap: wrap; }
    .who { display: flex; gap: 0.45rem; align-items: baseline; flex-wrap: wrap; min-width: 0; }
    .team { color: var(--color-text-secondary); font-size: 0.85rem; }
    .verdict { font-weight: 700; font-size: 0.9rem; }
    .said { margin: 0; font-size: 1.05rem; overflow-wrap: anywhere; }
    .actions { display: flex; flex-wrap: wrap; gap: 0.4rem; }
    .btn { border: 1px solid var(--color-border); background: transparent; color: inherit; border-radius: 999px; padding: 0.35rem 0.8rem; font-weight: 700; font-size: 0.85rem; cursor: pointer; }
    .btn:disabled { opacity: 0.55; cursor: default; }
    .btn.primary { background: var(--color-primary); border-color: var(--color-primary); color: #fff; }
    .btn.danger { border-color: var(--color-danger, #dc2626); color: var(--color-danger, #dc2626); }
    .btn.ghost { border-color: transparent; justify-self: start; }
    .picker { display: grid; gap: 0.45rem; padding: 0.6rem; border-radius: 10px; background: color-mix(in srgb, var(--color-primary) 8%, transparent); }
    .pick-title { margin: 0; font-weight: 700; font-size: 0.9rem; overflow-wrap: anywhere; }
    .chips { display: flex; flex-wrap: wrap; gap: 0.4rem; }
    .chip { display: inline-flex; align-items: center; gap: 0.4rem; border: 1px solid var(--color-border); background: var(--color-surface); color: inherit; border-radius: 10px; padding: 0.4rem 0.6rem; cursor: pointer; font-weight: 600; max-width: 100%; text-align: left; overflow-wrap: anywhere; }
    .chip .rank { font-weight: 900; color: var(--color-primary); flex-shrink: 0; }
    .chip .pts { font-size: 0.8rem; color: var(--color-text-secondary); flex-shrink: 0; white-space: nowrap; margin-left: auto; }
    .also { display: flex; gap: 0.4rem; align-items: center; font-size: 0.88rem; overflow-wrap: anywhere; }
    @media (max-width: 600px) {
      .btn { flex: 1 1 auto; text-align: center; }
      .chip { flex: 1 1 100%; }
    }
  `]
})
export class GameShowReviewPanelComponent implements OnDestroy {
  private readonly api = inject(GameShowApi);

  readonly sessionId = input.required<number>();
  readonly teamAName = input('Equipo A');
  readonly teamBName = input('Equipo B');
  readonly hideAnswers = input(false);
  /** Changes on every lobby update so the list follows the game. */
  readonly refreshKey = input(0);
  readonly lobbyChanged = output<GameShowLobbyDto>();

  readonly data = signal<GameShowHostReviewDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly busy = signal(false);
  readonly picker = signal<{ attemptId: number; kind: PickerKind } | null>(null);
  readonly alsoAlias = signal(true);
  private readonly manualRoundId = signal<number | null>(null);
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;
  private noticeTimer: ReturnType<typeof setTimeout> | null = null;

  readonly selectedRoundId = computed(() => {
    const d = this.data();
    if (!d?.rounds.length) return null;
    const manual = this.manualRoundId();
    if (manual !== null && d.rounds.some((r) => r.roundId === manual)) return manual;
    return (d.rounds.find((r) => r.isCurrent) ?? d.rounds[d.rounds.length - 1]).roundId;
  });

  readonly round = computed(() => {
    const id = this.selectedRoundId();
    return this.data()?.rounds.find((r) => r.roundId === id) ?? null;
  });

  constructor() {
    effect(() => {
      this.refreshKey();
      const id = this.sessionId();
      untracked(() => this.scheduleLoad(id));
    });
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    if (this.noticeTimer) clearTimeout(this.noticeTimer);
  }

  pickRound(value: string): void {
    const id = Number(value);
    const current = this.data()?.rounds.find((r) => r.isCurrent)?.roundId ?? null;
    this.manualRoundId.set(id === current ? null : id);
    this.picker.set(null);
  }

  teamName(team: string): string {
    return team === 'A' ? this.teamAName() : team === 'B' ? this.teamBName() : team;
  }

  answerLabel(r: GameShowReviewRoundDto, answerId: number | null): string {
    const ans = r.answers.find((x) => x.id === answerId);
    return ans ? `${ans.text} (${ans.points})` : 'Correcta';
  }

  aliasTarget(r: GameShowReviewRoundDto): string {
    const d = this.data();
    if (d?.canEditPack) return 'tu pack de preguntas (queda para siempre)';
    if (r.isOfficialQuestion && d?.canEditOfficial) return 'el paquete oficial (queda para siempre)';
    if (r.isOfficialQuestion) return 'solo esta partida (el paquete oficial lo edita el administrador)';
    return 'solo esta partida';
  }

  pickerAnswers(r: GameShowReviewRoundDto, a: GameShowReviewAttemptDto): GameShowReviewAnswerDto[] {
    const kind = this.picker()?.kind;
    if (kind === 'accept' && a.acceptMode !== 'Steal') {
      return r.answers.filter((x) => !x.isRevealed);
    }
    return r.answers;
  }

  openPicker(a: GameShowReviewAttemptDto, kind: PickerKind): void {
    const open = this.picker();
    this.picker.set(open?.attemptId === a.id && open.kind === kind ? null : { attemptId: a.id, kind });
    this.alsoAlias.set(true);
  }

  choose(r: GameShowReviewRoundDto, a: GameShowReviewAttemptDto, ans: GameShowReviewAnswerDto): void {
    const kind = this.picker()?.kind;
    if (kind === 'alias') {
      this.saveAlias(r, a, ans);
      return;
    }

    this.busy.set(true);
    this.api.acceptAttempt(this.sessionId(), a.id, ans.id).subscribe({
      next: (lobby) => {
        this.lobbyChanged.emit(lobby);
        this.picker.set(null);
        if (this.alsoAlias()) {
          this.saveAlias(r, a, ans, `Aceptada como «${ans.text}».`);
        } else {
          this.busy.set(false);
          this.showNotice(`Aceptada como «${ans.text}».`);
          this.load();
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
        this.load();
      }
    });
  }

  reject(a: GameShowReviewAttemptDto): void {
    if (!confirm(`¿Marcar «${a.text}» como incorrecta? Se ocultará la respuesta y el equipo recibirá una X.`)) return;
    this.busy.set(true);
    this.api.rejectAttempt(this.sessionId(), a.id).subscribe({
      next: (lobby) => {
        this.busy.set(false);
        this.lobbyChanged.emit(lobby);
        this.showNotice('Marcada como incorrecta.');
        this.load();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(mapApiError(err));
        this.load();
      }
    });
  }

  private saveAlias(
    r: GameShowReviewRoundDto,
    a: GameShowReviewAttemptDto,
    ans: GameShowReviewAnswerDto,
    prefix = ''
  ): void {
    this.busy.set(true);
    this.api.addAlias(this.sessionId(), r.roundId, ans.id, a.text).subscribe({
      next: (res) => {
        this.busy.set(false);
        this.picker.set(null);
        this.showNotice(`${prefix} ${res.message}`.trim());
        this.load();
      },
      error: (err) => {
        this.busy.set(false);
        const msg = mapApiError(err);
        this.error.set(prefix ? `${prefix} Pero el sinónimo no se guardó: ${msg}` : msg);
        this.load();
      }
    });
  }

  private scheduleLoad(id: number): void {
    if (!id) return;
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    this.refreshTimer = setTimeout(() => this.load(), 250);
  }

  private load(): void {
    this.api.review(this.sessionId()).subscribe({
      next: (d) => {
        this.data.set(d);
        const open = this.picker();
        if (open && !d.rounds.some((r) => r.attempts.some((x) => x.id === open.attemptId))) {
          this.picker.set(null);
        }
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private showNotice(message: string): void {
    this.error.set(null);
    this.notice.set(message);
    if (this.noticeTimer) clearTimeout(this.noticeTimer);
    this.noticeTimer = setTimeout(() => this.notice.set(null), 6000);
  }
}
