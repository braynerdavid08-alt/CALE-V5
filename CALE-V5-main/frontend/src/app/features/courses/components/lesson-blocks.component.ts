import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Observable, of } from 'rxjs';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import {
  ActivityAnswer,
  ClassifyBlock,
  FillBlankBlock,
  FlipcardsBlock,
  HotspotBlock,
  LessonBlock,
  MatchBlock,
  MediaBlock,
  OrderBlock,
  QuizBlock,
  QuizCheckResult,
  ScenarioBlock,
  SignsBlock,
  TextBlock,
  TrueFalseBlock
} from '../api/courses.api';

export type QuizChecker = (blockIndex: number, answer: ActivityAnswer) => Observable<QuizCheckResult>;

export interface ActivityAnswered {
  blockIndex: number;
  value: ActivityAnswer;
  correct: boolean;
}

interface ActState {
  chosen?: number;
  items?: string[];
  values?: string[];
  focus?: number;
  choice?: (number | null)[];
  result: QuizCheckResult | null;
  busy: boolean;
}

interface MatchState {
  order: number[];
  matched: Set<number>;
  pickedLeft: number | null;
  wrongRight: number | null;
}

interface HotspotState {
  found: Set<number>;
  miss: { x: number; y: number } | null;
  showAll: boolean;
}

const BLANK = /\[\[(.+?)\]\]/g;

function shuffled(count: number, avoidIdentity: boolean): number[] {
  const order = Array.from({ length: count }, (_, k) => k);
  for (let k = order.length - 1; k > 0; k--) {
    const j = Math.floor(Math.random() * (k + 1));
    [order[k], order[j]] = [order[j], order[k]];
  }
  if (avoidIdentity && count > 1 && order.every((v, k) => v === k)) order.push(order.shift()!);
  return order;
}

function fold(v: string): string {
  return (v ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim().replace(/\s+/g, ' ').replace(/\.+$/, '');
}

function blanksOf(text: string): string[][] {
  return [...(text ?? '').matchAll(BLANK)].map((m) => m[1].split('|').map((s) => s.trim()).filter(Boolean));
}

/** Renders lesson blocks for students and for the editor preview. */
@Component({
  selector: 'course-lesson-blocks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet],
  templateUrl: './lesson-blocks.component.html',
  styleUrl: './lesson-blocks.component.css'
})
export class LessonBlocksComponent implements OnChanges {
  private readonly sanitizer = inject(DomSanitizer);

  @Input({ required: true }) blocks: LessonBlock[] = [];
  /** Checks answers on the server. Without it (editor preview) the answers inside the blocks are used. */
  @Input() checker: QuizChecker | null = null;
  /** Block indexes to show (step-by-step player). Null shows every block. */
  @Input() only: number[] | null = null;
  @Output() readonly answered = new EventEmitter<ActivityAnswered>();

  readonly letters = ['A', 'B', 'C', 'D', 'E', 'F'];
  readonly tfOptions = ['Verdadero', 'Falso'];
  view: LessonBlock[] = [];
  private classifyOrder: Record<number, number[]> = {};
  private readonly acts = signal<Record<number, ActState>>({});
  private readonly flipped = signal<Set<string>>(new Set());
  private readonly matches = signal<Record<number, MatchState>>({});
  private readonly hotspots = signal<Record<number, HotspotState>>({});
  private readonly ytCache = new Map<string, SafeResourceUrl | null>();

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['blocks'] && !changes['checker']) return;
    this.classifyOrder = {};
    this.view = (this.blocks ?? []).map((b, i) => (this.checker ? b : this.previewBlock(b, i)));
    const acts: Record<number, ActState> = {};
    const matches: Record<number, MatchState> = {};
    const hotspots: Record<number, HotspotState> = {};
    this.view.forEach((b, i) => {
      if (b.type === 'match') matches[i] = this.newMatch(b.pairs.length);
      if (b.type === 'hotspot') hotspots[i] = { found: new Set(), miss: null, showAll: false };
      if (b.type === 'order') acts[i] = { items: [...b.steps], result: null, busy: false };
      if (b.type === 'fillblank') acts[i] = { values: (b.parts ?? []).slice(1).map(() => ''), focus: 0, result: null, busy: false };
      if (b.type === 'classify') acts[i] = { choice: b.items.map(() => null), result: null, busy: false };
    });
    this.acts.set(acts);
    this.flipped.set(new Set());
    this.matches.set(matches);
    this.hotspots.set(hotspots);
  }

  asText(b: LessonBlock): TextBlock { return b as TextBlock; }
  asMedia(b: LessonBlock): MediaBlock { return b as MediaBlock; }
  asSigns(b: LessonBlock): SignsBlock { return b as SignsBlock; }
  asQuiz(b: LessonBlock): QuizBlock { return b as QuizBlock; }
  asCards(b: LessonBlock): FlipcardsBlock { return b as FlipcardsBlock; }
  asMatch(b: LessonBlock): MatchBlock { return b as MatchBlock; }
  asTf(b: LessonBlock): TrueFalseBlock { return b as TrueFalseBlock; }
  asOrder(b: LessonBlock): OrderBlock { return b as OrderBlock; }
  asHotspot(b: LessonBlock): HotspotBlock { return b as HotspotBlock; }
  asScenario(b: LessonBlock): ScenarioBlock { return b as ScenarioBlock; }
  asFill(b: LessonBlock): FillBlankBlock { return b as FillBlankBlock; }
  asClassify(b: LessonBlock): ClassifyBlock { return b as ClassifyBlock; }

  url(path?: string | null): string {
    return resolveMediaUrl(path);
  }

  choiceTexts(b: ScenarioBlock): string[] {
    return b.choices.map((c) => c.text);
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

  act(i: number): ActState | undefined {
    return this.acts()[i];
  }

  // ── Single choice: quiz, true/false, scenario ───────────────────────

  choose(i: number, option: number): void {
    const s = this.act(i);
    if (s?.result || s?.busy) return;
    this.submit(i, option, { chosen: option });
  }

  // ── Order ───────────────────────────────────────────────────────────

  move(i: number, k: number, delta: number): void {
    const s = this.act(i);
    if (!s?.items || s.result) return;
    const items = [...s.items];
    const to = k + delta;
    if (to < 0 || to >= items.length) return;
    [items[k], items[to]] = [items[to], items[k]];
    this.setAct(i, { ...s, items });
  }

  stepRight(i: number, k: number): boolean | null {
    const s = this.act(i);
    const sol = s?.result?.solution;
    return sol && s?.items ? fold(String(sol[k])) === fold(s.items[k]) : null;
  }

  // ── Fill in the blanks ──────────────────────────────────────────────

  setFill(i: number, k: number, event: Event): void {
    const s = this.act(i);
    if (!s?.values || s.result) return;
    const values = [...s.values];
    values[k] = (event.target as HTMLInputElement).value;
    this.setAct(i, { ...s, values, focus: k });
  }

  focusFill(i: number, k: number): void {
    const s = this.act(i);
    if (s && s.focus !== k) this.setAct(i, { ...s, focus: k });
  }

  useWord(i: number, word: string): void {
    const s = this.act(i);
    if (!s?.values || s.result) return;
    const values = [...s.values];
    let k = s.focus ?? 0;
    if (values[k]) {
      const empty = values.findIndex((v) => !v);
      if (empty >= 0) k = empty;
    }
    values[k] = word;
    const nextEmpty = values.findIndex((v) => !v);
    this.setAct(i, { ...s, values, focus: nextEmpty >= 0 ? nextEmpty : k });
  }

  blankRight(i: number, k: number): boolean | null {
    const s = this.act(i);
    if (!s?.result || !s.values) return null;
    if (s.result.correct) return true;
    return fold(s.values[k]) === fold(String(s.result.solution?.[k] ?? ''));
  }

  // ── Classify ────────────────────────────────────────────────────────

  classify(i: number, k: number, group: number): void {
    const s = this.act(i);
    if (!s?.choice || s.result) return;
    const choice = [...s.choice];
    choice[k] = group;
    this.setAct(i, { ...s, choice });
  }

  classifyReady(i: number): boolean {
    return !!this.act(i)?.choice?.every((c) => c !== null);
  }

  classifyRight(i: number, k: number): boolean | null {
    const s = this.act(i);
    return s?.result?.solution && s.choice ? s.result.solution[k] === s.choice[k] : null;
  }

  expectedGroup(i: number, k: number): number {
    return Number(this.act(i)?.result?.solution?.[k] ?? -1);
  }

  // ── Shared submit for order, fill-in and classify ───────────────────

  check(i: number): void {
    const s = this.act(i);
    if (!s || s.result || s.busy) return;
    const b = this.view[i];
    const answer: ActivityAnswer = b.type === 'order'
      ? [...(s.items ?? [])]
      : b.type === 'fillblank'
        ? (s.values ?? []).map((v) => v.trim())
        : (s.choice ?? []).map((c) => c ?? -1);
    this.submit(i, answer, {});
  }

  // ── Flip cards and match ────────────────────────────────────────────

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

  // ── Hotspot ─────────────────────────────────────────────────────────

  hot(i: number): HotspotState | undefined {
    return this.hotspots()[i];
  }

  tapImage(i: number, event: MouseEvent): void {
    const h = this.hot(i);
    const block = this.view[i] as HotspotBlock;
    if (!h) return;
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * 100;
    const y = ((event.clientY - rect.top) / rect.height) * 100;
    let best = -1;
    let bestDist = 9;
    block.spots.forEach((s, k) => {
      const d = Math.hypot(s.x - x, s.y - y);
      if (d < bestDist) {
        bestDist = d;
        best = k;
      }
    });
    if (best >= 0) {
      const found = new Set(h.found);
      found.add(best);
      this.hotspots.update((all) => ({ ...all, [i]: { ...h, found, miss: null } }));
    } else {
      this.hotspots.update((all) => ({ ...all, [i]: { ...h, miss: { x, y } } }));
      setTimeout(() => {
        const now = this.hotspots()[i];
        if (now?.miss?.x === x) this.hotspots.update((all) => ({ ...all, [i]: { ...now, miss: null } }));
      }, 600);
    }
  }

  showAllSpots(i: number): void {
    const h = this.hot(i);
    if (h) this.hotspots.update((all) => ({ ...all, [i]: { ...h, showAll: true } }));
  }

  spotVisible(i: number, k: number): boolean {
    const h = this.hot(i);
    return !!h && (h.showAll || h.found.has(k));
  }

  // ── Internals ───────────────────────────────────────────────────────

  private submit(i: number, answer: ActivityAnswer, patch: Partial<ActState>): void {
    const s = this.act(i) ?? { result: null, busy: false };
    this.setAct(i, { ...s, ...patch, busy: true });
    const check$ = this.checker ? this.checker(i, answer) : of(this.localCheck(i, answer));
    check$.subscribe({
      next: (result) => {
        this.setAct(i, { ...this.act(i)!, result, busy: false });
        this.answered.emit({ blockIndex: i, value: answer, correct: result.correct });
      },
      error: () => this.setAct(i, { ...this.act(i)!, busy: false })
    });
  }

  /** Editor preview: grade with the answers stored in the block. */
  private localCheck(i: number, answer: ActivityAnswer): QuizCheckResult {
    const b = this.blocks[i];
    switch (b.type) {
      case 'quiz':
        return { correct: answer === b.correct, correctIndex: b.correct ?? 0, explanation: b.explanation };
      case 'truefalse': {
        const correct = b.answer ? 0 : 1;
        return { correct: answer === correct, correctIndex: correct, explanation: b.explanation };
      }
      case 'scenario': {
        const best = Math.max(0, b.choices.findIndex((c) => c.best));
        const chosen = b.choices[answer as number];
        return {
          correct: !!chosen?.best,
          correctIndex: best,
          explanation: chosen?.outcome,
          solution: b.choices.map((c) => c.outcome ?? '')
        };
      }
      case 'order': {
        const given = answer as string[];
        const correct = given.length === b.steps.length && b.steps.every((s, k) => fold(s) === fold(given[k]));
        return { correct, correctIndex: -1, explanation: b.explanation, solution: b.steps };
      }
      case 'fillblank': {
        const blanks = blanksOf(b.text ?? '');
        const given = answer as string[];
        const correct = blanks.length === given.length && blanks.every((alts, k) => alts.some((a) => fold(a) === fold(given[k])));
        return { correct, correctIndex: -1, explanation: b.explanation, solution: blanks.map((alts) => alts[0] ?? '') };
      }
      case 'classify': {
        const order = this.classifyOrder[i] ?? b.items.map((_, k) => k);
        const expected = order.map((o) => b.items[o].group ?? 0);
        const given = answer as number[];
        const correct = expected.every((g, k) => g === given[k]);
        return { correct, correctIndex: -1, explanation: b.explanation, solution: expected };
      }
      default:
        return { correct: false, correctIndex: -1 };
    }
  }

  private previewBlock(b: LessonBlock, i: number): LessonBlock {
    switch (b.type) {
      case 'order': {
        const order = shuffled(b.steps.length, true);
        return { ...b, steps: order.map((o) => b.steps[o]) };
      }
      case 'fillblank': {
        const text = b.text ?? '';
        const parts = text.split(/\[\[.+?\]\]/);
        const words = [...new Set([...blanksOf(text).map((alts) => alts[0] ?? ''), ...(b.distractors ?? [])]
          .map((w) => w.trim()).filter(Boolean))];
        const order = shuffled(words.length, true);
        return { ...b, parts, bank: order.map((o) => words[o]) };
      }
      case 'classify': {
        const order = shuffled(b.items.length, false);
        this.classifyOrder[i] = order;
        return { ...b, items: order.map((o) => ({ text: b.items[o].text, imageUrl: b.items[o].imageUrl })) };
      }
      default:
        return b;
    }
  }

  private newMatch(count: number): MatchState {
    return { order: shuffled(count, true), matched: new Set(), pickedLeft: null, wrongRight: null };
  }

  private setAct(i: number, state: ActState): void {
    this.acts.update((all) => ({ ...all, [i]: state }));
  }

  private setMatch(i: number, state: MatchState): void {
    this.matches.update((all) => ({ ...all, [i]: state }));
  }
}
