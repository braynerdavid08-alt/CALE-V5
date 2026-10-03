import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Observable, of } from 'rxjs';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import {
  FlipcardsBlock,
  LessonBlock,
  MatchBlock,
  MediaBlock,
  QuizBlock,
  QuizCheckResult,
  SignsBlock,
  TextBlock
} from '../api/courses.api';

export type QuizChecker = (blockIndex: number, option: number) => Observable<QuizCheckResult>;

interface QuizState {
  chosen: number;
  result: QuizCheckResult | null;
  busy: boolean;
}

interface MatchState {
  order: number[];
  matched: Set<number>;
  pickedLeft: number | null;
  wrongRight: number | null;
}

/** Renders lesson blocks for students and for the editor preview. */
@Component({
  selector: 'course-lesson-blocks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (block of blocks; track $index; let i = $index) {
      <section class="blk" [attr.data-type]="block.type">
        @switch (block.type) {
          @case ('text') {
            @if (asText(block).title) {
              <h2>{{ asText(block).title }}</h2>
            }
            @for (p of paragraphs(asText(block).body); track $index) {
              <p class="para">{{ p }}</p>
            }
          }
          @case ('tip') {
            <aside class="tip"><span aria-hidden="true">💡</span><p>{{ asText(block).body }}</p></aside>
          }
          @case ('image') {
            <figure>
              <img [src]="url(asMedia(block).url)" [alt]="asMedia(block).caption || ''" loading="lazy" />
              @if (asMedia(block).caption) { <figcaption>{{ asMedia(block).caption }}</figcaption> }
            </figure>
          }
          @case ('video') {
            <figure>
              @if (youtube(asMedia(block).url); as yt) {
                <iframe class="video" [src]="yt" title="Video" allowfullscreen
                  allow="accelerometer; encrypted-media; gyroscope; picture-in-picture"></iframe>
              } @else {
                <video class="video" [src]="url(asMedia(block).url)" controls preload="metadata" playsinline></video>
              }
              @if (asMedia(block).caption) { <figcaption>{{ asMedia(block).caption }}</figcaption> }
            </figure>
          }
          @case ('audio') {
            <figure class="audio">
              <audio [src]="url(asMedia(block).url)" controls preload="none"></audio>
              @if (asMedia(block).caption) { <figcaption>{{ asMedia(block).caption }}</figcaption> }
            </figure>
          }
          @case ('signs') {
            @if (asSigns(block).title) { <h2>{{ asSigns(block).title }}</h2> }
            <div class="signs">
              @for (s of asSigns(block).items; track s.code) {
                <article class="sign">
                  <img [src]="url('/signals/' + s.code + '.svg')" [alt]="s.name" loading="lazy" />
                  <div>
                    <strong>{{ s.name }}</strong>
                    <small class="code">{{ s.code }}</small>
                    @if (s.note) { <p>{{ s.note }}</p> }
                  </div>
                </article>
              }
            </div>
          }
          @case ('quiz') {
            <div class="quiz">
              <p class="kicker">Pregunta</p>
              <h3>{{ asQuiz(block).question }}</h3>
              @if (asQuiz(block).imageUrl) {
                <img class="quiz-img" [src]="url(asQuiz(block).imageUrl)" alt="" loading="lazy" />
              }
              <div class="options">
                @for (opt of asQuiz(block).options; track $index; let o = $index) {
                  <button
                    type="button"
                    class="opt"
                    [class.right]="quiz(i)?.result && quiz(i)!.result!.correctIndex === o"
                    [class.wrong]="quiz(i)?.result && !quiz(i)!.result!.correct && quiz(i)!.chosen === o"
                    [disabled]="!!quiz(i)?.result || quiz(i)?.busy"
                    (click)="answer(i, o)">
                    <span class="letter">{{ letters[o] }}</span>
                    <span>{{ opt }}</span>
                  </button>
                }
              </div>
              @if (quiz(i)?.result; as r) {
                <p class="feedback" [class.ok]="r.correct" role="status">
                  <strong>{{ r.correct ? '¡Correcto!' : 'No es correcto.' }}</strong>
                  @if (r.explanation) { {{ r.explanation }} }
                </p>
              }
            </div>
          }
          @case ('flipcards') {
            @if (asCards(block).title) { <h2>{{ asCards(block).title }}</h2> }
            <div class="cards">
              @for (c of asCards(block).cards; track $index; let ci = $index) {
                <button type="button" class="card" [class.flipped]="isFlipped(i, ci)" (click)="flip(i, ci)"
                  [attr.aria-label]="isFlipped(i, ci) ? c.back : c.front + '. Toca para ver la respuesta'">
                  <span class="face front">
                    @if (c.imageUrl) { <img [src]="url(c.imageUrl)" alt="" loading="lazy" /> }
                    <strong>{{ c.front }}</strong>
                    <small>Toca para voltear</small>
                  </span>
                  <span class="face back"><span>{{ c.back }}</span></span>
                </button>
              }
            </div>
          }
          @case ('match') {
            @if (matchState(i); as m) {
              <p class="kicker">Actividad</p>
              <h3>{{ asMatch(block).instructions || 'Une cada elemento con su pareja.' }}</h3>
              <div class="match">
                <div class="col">
                  @for (p of asMatch(block).pairs; track $index; let li = $index) {
                    <button type="button" class="m-item left"
                      [class.done]="m.matched.has(li)"
                      [class.picked]="m.pickedLeft === li"
                      [disabled]="m.matched.has(li)"
                      (click)="pickLeft(i, li)">
                      @if (p.imageUrl) { <img [src]="url(p.imageUrl)" [alt]="p.left || 'Imagen ' + (li + 1)" /> }
                      @if (p.left) { <span>{{ p.left }}</span> }
                    </button>
                  }
                </div>
                <div class="col">
                  @for (ri of m.order; track ri) {
                    <button type="button" class="m-item right"
                      [class.done]="m.matched.has(ri)"
                      [class.wrong]="m.wrongRight === ri"
                      [disabled]="m.matched.has(ri) || m.pickedLeft === null"
                      (click)="pickRight(i, ri)">
                      {{ asMatch(block).pairs[ri].right }}
                    </button>
                  }
                </div>
              </div>
              @if (m.matched.size === asMatch(block).pairs.length) {
                <p class="feedback ok" role="status"><strong>¡Muy bien!</strong> Uniste todas las parejas.</p>
              } @else {
                <p class="hint">Toca un elemento de la izquierda y luego su pareja de la derecha.</p>
              }
            }
          }
        }
      </section>
    }
  `,
  styles: [`
    :host { display: grid; gap: 1.25rem; }
    .blk { min-width: 0; }
    h2 { margin: 0 0 0.6rem; font-size: var(--text-lg); }
    h3 { margin: 0.15rem 0 0.75rem; font-size: var(--text-md); line-height: 1.4; }
    .para { margin: 0 0 0.75rem; line-height: var(--leading-body, 1.6); white-space: pre-line; }
    .kicker { margin: 0; font-size: var(--text-xs); font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; color: var(--color-primary); }
    .tip { display: flex; gap: 0.7rem; padding: 0.9rem 1rem; border-radius: var(--radius-md); background: var(--color-warning-soft); border-left: 4px solid var(--color-warning); }
    .tip p { margin: 0; line-height: 1.55; }
    figure { margin: 0; display: grid; gap: 0.4rem; }
    figure img, .video { width: 100%; max-height: 70vh; object-fit: contain; border-radius: var(--radius-md); background: var(--color-chip); }
    .video { aspect-ratio: 16 / 9; border: 0; }
    audio { width: 100%; }
    figcaption { font-size: var(--text-sm); color: var(--color-text-secondary); text-align: center; }
    .signs { display: grid; grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr)); gap: 0.75rem; }
    .sign { display: flex; gap: 0.75rem; align-items: flex-start; padding: 0.75rem; border-radius: var(--radius-md); border: 1px solid var(--color-border); background: var(--color-surface); }
    .sign img { width: 4.2rem; height: 4.2rem; object-fit: contain; flex: none; }
    .sign strong { display: block; line-height: 1.3; }
    .sign .code { color: var(--color-text-secondary); font-size: var(--text-xs); }
    .sign p { margin: 0.3rem 0 0; font-size: var(--text-sm); color: var(--color-text-secondary); line-height: 1.45; }
    .quiz { padding: 1rem; border-radius: var(--radius-lg); border: 1px solid color-mix(in srgb, var(--color-primary) 30%, var(--color-border)); background: color-mix(in srgb, var(--color-primary) 5%, var(--color-surface)); }
    .quiz-img { display: block; max-width: 11rem; max-height: 11rem; margin: 0 auto 0.75rem; object-fit: contain; }
    .options { display: grid; gap: 0.5rem; }
    .opt { display: flex; align-items: center; gap: 0.7rem; width: 100%; padding: 0.75rem 0.9rem; text-align: left; font: inherit; color: inherit; border-radius: var(--radius-md); border: 1px solid var(--color-border); background: var(--color-surface); cursor: pointer; }
    .opt:not(:disabled):hover { border-color: var(--color-primary); }
    .opt:disabled { cursor: default; }
    .opt.right { border-color: var(--color-success); background: var(--color-success-soft); }
    .opt.wrong { border-color: var(--color-danger); background: var(--color-danger-soft); }
    .letter { display: grid; place-items: center; flex: none; width: 1.8rem; height: 1.8rem; border-radius: 50%; font-weight: 800; font-size: var(--text-sm); background: var(--color-chip); }
    .feedback { margin: 0.75rem 0 0; padding: 0.7rem 0.9rem; border-radius: var(--radius-md); background: var(--color-danger-soft); line-height: 1.5; }
    .feedback.ok { background: var(--color-success-soft); }
    .hint { margin: 0.6rem 0 0; font-size: var(--text-sm); color: var(--color-text-secondary); }
    .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(12rem, 1fr)); gap: 0.75rem; }
    .card { position: relative; min-height: 12rem; padding: 0; border: 0; background: none; cursor: pointer; perspective: 900px; font: inherit; color: inherit; }
    .face { position: absolute; inset: 0; display: grid; place-items: center; align-content: center; gap: 0.4rem; padding: 0.9rem; text-align: center; border-radius: var(--radius-lg); border: 1px solid var(--color-border); backface-visibility: hidden; transition: transform 0.45s ease; }
    .front { background: var(--color-surface); }
    .front img { width: 5rem; height: 5rem; object-fit: contain; }
    .front small { color: var(--color-text-secondary); font-size: var(--text-xs); }
    .back { transform: rotateY(180deg); background: color-mix(in srgb, var(--color-primary) 12%, var(--color-surface)); line-height: 1.45; }
    .card.flipped .front { transform: rotateY(180deg); }
    .card.flipped .back { transform: rotateY(360deg); }
    .match { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .col { display: grid; gap: 0.5rem; align-content: start; }
    .m-item { display: flex; align-items: center; gap: 0.6rem; min-height: 3.6rem; padding: 0.55rem 0.75rem; text-align: left; font: inherit; color: inherit; border-radius: var(--radius-md); border: 1px solid var(--color-border); background: var(--color-surface); cursor: pointer; }
    .m-item img { width: 3rem; height: 3rem; object-fit: contain; flex: none; }
    .m-item.picked { border-color: var(--color-primary); box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-primary) 35%, transparent); }
    .m-item.done { border-color: var(--color-success); background: var(--color-success-soft); cursor: default; }
    .m-item.wrong { border-color: var(--color-danger); background: var(--color-danger-soft); }
    .m-item.right:disabled:not(.done) { opacity: 0.75; cursor: default; }
    @media (max-width: 520px) {
      .match { gap: 0.45rem; }
      .m-item { font-size: var(--text-sm); padding: 0.45rem 0.55rem; }
      .m-item img { width: 2.4rem; height: 2.4rem; }
    }
  `]
})
export class LessonBlocksComponent implements OnChanges {
  private readonly sanitizer = inject(DomSanitizer);

  @Input({ required: true }) blocks: LessonBlock[] = [];
  /** Checks a quiz answer on the server. Without it, the answer in the block is used (editor preview). */
  @Input() checker: QuizChecker | null = null;
  @Output() readonly answered = new EventEmitter<{ blockIndex: number; option: number; correct: boolean }>();

  readonly letters = ['A', 'B', 'C', 'D', 'E', 'F'];
  private readonly quizzes = signal<Record<number, QuizState>>({});
  private readonly flipped = signal<Set<string>>(new Set());
  private readonly matches = signal<Record<number, MatchState>>({});
  private readonly ytCache = new Map<string, SafeResourceUrl | null>();

  ngOnChanges(): void {
    this.quizzes.set({});
    this.flipped.set(new Set());
    const matches: Record<number, MatchState> = {};
    (this.blocks ?? []).forEach((b, i) => {
      if (b.type === 'match') matches[i] = this.newMatch(b.pairs.length);
    });
    this.matches.set(matches);
  }

  asText(b: LessonBlock): TextBlock { return b as TextBlock; }
  asMedia(b: LessonBlock): MediaBlock { return b as MediaBlock; }
  asSigns(b: LessonBlock): SignsBlock { return b as SignsBlock; }
  asQuiz(b: LessonBlock): QuizBlock { return b as QuizBlock; }
  asCards(b: LessonBlock): FlipcardsBlock { return b as FlipcardsBlock; }
  asMatch(b: LessonBlock): MatchBlock { return b as MatchBlock; }

  url(path?: string | null): string {
    return resolveMediaUrl(path);
  }

  paragraphs(body: string): string[] {
    return (body ?? '').split(/\n\s*\n/).map((p) => p.trim()).filter(Boolean);
  }

  youtube(raw: string): SafeResourceUrl | null {
    if (this.ytCache.has(raw)) return this.ytCache.get(raw)!;
    const m = /(?:youtube\.com\/(?:watch\?v=|embed\/|shorts\/)|youtu\.be\/)([\w-]{11})/i.exec(raw ?? '');
    const safe = m
      ? this.sanitizer.bypassSecurityTrustResourceUrl(`https://www.youtube-nocookie.com/embed/${m[1]}`)
      : null;
    this.ytCache.set(raw, safe);
    return safe;
  }

  quiz(i: number): QuizState | undefined {
    return this.quizzes()[i];
  }

  answer(i: number, option: number): void {
    if (this.quiz(i)?.result || this.quiz(i)?.busy) return;
    const block = this.blocks[i] as QuizBlock;
    this.setQuiz(i, { chosen: option, result: null, busy: true });
    const check$ = this.checker
      ? this.checker(i, option)
      : of<QuizCheckResult>({
          correct: option === block.correct,
          correctIndex: block.correct ?? 0,
          explanation: block.explanation
        });
    check$.subscribe({
      next: (result) => {
        this.setQuiz(i, { chosen: option, result, busy: false });
        this.answered.emit({ blockIndex: i, option, correct: result.correct });
      },
      error: () => this.setQuiz(i, { chosen: option, result: null, busy: false })
    });
  }

  isFlipped(i: number, ci: number): boolean {
    return this.flipped().has(`${i}:${ci}`);
  }

  flip(i: number, ci: number): void {
    const next = new Set(this.flipped());
    const key = `${i}:${ci}`;
    if (next.has(key)) next.delete(key); else next.add(key);
    this.flipped.set(next);
  }

  matchState(i: number): MatchState | undefined {
    return this.matches()[i];
  }

  pickLeft(i: number, li: number): void {
    const m = this.matchState(i);
    if (!m) return;
    this.setMatch(i, { ...m, pickedLeft: m.pickedLeft === li ? null : li, wrongRight: null });
  }

  pickRight(i: number, ri: number): void {
    const m = this.matchState(i);
    if (!m || m.pickedLeft === null) return;
    if (m.pickedLeft === ri) {
      const matched = new Set(m.matched);
      matched.add(ri);
      this.setMatch(i, { ...m, matched, pickedLeft: null, wrongRight: null });
    } else {
      this.setMatch(i, { ...m, wrongRight: ri });
      setTimeout(() => {
        const now = this.matches()[i];
        if (now?.wrongRight === ri) this.setMatch(i, { ...now, wrongRight: null });
      }, 700);
    }
  }

  private newMatch(count: number): MatchState {
    const order = Array.from({ length: count }, (_, k) => k);
    for (let k = order.length - 1; k > 0; k--) {
      const j = Math.floor(Math.random() * (k + 1));
      [order[k], order[j]] = [order[j], order[k]];
    }
    if (count > 1 && order.every((v, k) => v === k)) order.reverse();
    return { order, matched: new Set(), pickedLeft: null, wrongRight: null };
  }

  private setQuiz(i: number, state: QuizState): void {
    this.quizzes.update((all) => ({ ...all, [i]: state }));
  }

  private setMatch(i: number, state: MatchState): void {
    this.matches.update((all) => ({ ...all, [i]: state }));
  }
}
