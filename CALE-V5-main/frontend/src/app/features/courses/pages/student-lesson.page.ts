import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { Badge } from '../../play/api/play.api';
import { PlayBadgesToastComponent } from '../../play/components/play-badges-toast.component';
import { PlayFxService } from '../../play/play-fx.service';
import { ActivityAnswer, LessonCompleteResult, StudentCoursesApi, StudentLesson } from '../api/courses.api';
import { ActivityAnswered, LessonBlocksComponent, QuizChecker } from '../components/lesson-blocks.component';

const GRADED = new Set<string | undefined>(['quiz', 'truefalse', 'scenario', 'order', 'fillblank', 'classify']);
const STOPS = new Set<string | undefined>([...GRADED, 'match', 'hotspot', 'flipcards', 'video']);

/** Each step ends at an activity or video; long reading runs are cut every two blocks. */
export function splitSteps(blocks: { type: string }[]): number[][] {
  const steps: number[][] = [];
  let current: number[] = [];
  let reading = 0;
  blocks.forEach((b, i) => {
    current.push(i);
    if (STOPS.has(b.type)) {
      steps.push(current);
      current = [];
      reading = 0;
      return;
    }
    reading++;
    const next = blocks[i + 1];
    if (reading >= 2 && next && !STOPS.has(next.type)) {
      steps.push(current);
      current = [];
      reading = 0;
    }
  });
  if (current.length) steps.push(current);
  return steps.length ? steps : [[]];
}

@Component({
  selector: 'app-student-lesson-page',
  standalone: true,
  imports: [RouterLink, UiButtonComponent, UiLoadingComponent, LessonBlocksComponent, PlayBadgesToastComponent],
  styleUrl: './courses.css',
  template: `
    <section class="courses-page narrow">
      @if (lesson(); as l) {
        <a class="back-link" [routerLink]="['/student/cursos', l.courseId]">← {{ l.courseTitle }}</a>
      }

      @if (error()) {
        <p class="alert" role="alert">{{ error() }}</p>
      }

      @if (loading()) {
        <ui-loading />
      }
      @if (!loading() && lesson(); as l) {
        <header class="panel">
          <span class="tag">Lección {{ l.position }} de {{ l.totalLessons }} · {{ l.estimatedMinutes }} min</span>
          <h1 style="margin: 0.5rem 0 0.25rem; font-size: clamp(1.35rem, 3vw, 1.8rem)">{{ l.title }}</h1>
          @if (l.summary) { <p class="muted" style="margin: 0">{{ l.summary }}</p> }
          @if (l.completed) {
            <p class="ok-msg" style="margin-top: 0.75rem">Ya completaste esta lección (mejor nota {{ l.bestScore }}%). Puedes repasarla cuando quieras.</p>
          }
        </header>

        @if (!allAtOnce() && !result()) {
          <div class="stepper" role="group" aria-label="Avance de la lección">
            <div class="step-top">
              <strong>Paso {{ step() + 1 }} de {{ steps().length }}</strong>
              <button type="button" class="link-btn" (click)="allAtOnce.set(true)">Ver toda la lección</button>
            </div>
            <span class="bar"><span [style.width.%]="((step() + 1) / steps().length) * 100"></span></span>
          </div>

          <div class="guide" aria-live="polite">
            <span class="guide-face" aria-hidden="true">🚦</span>
            <p class="guide-bubble">{{ guideLine() }}</p>
          </div>
        }

        <div class="panel">
          <course-lesson-blocks
            [blocks]="l.content"
            [checker]="checker"
            [only]="allAtOnce() || result() ? null : steps()[step()]"
            (answered)="onAnswered($event)" />
        </div>

        @if (result(); as r) {
          <div class="panel result" role="status">
            <p class="big">{{ r.score }}%</p>
            <h2 style="margin: 0">{{ r.courseCompleted ? '¡Terminaste el curso!' : '¡Lección completada!' }}</h2>
            <p class="muted">
              Llevas {{ r.completedLessons }} de {{ r.totalLessons }} lecciones.
              @if (r.firstCompletion) { Ganaste experiencia para tu nivel. }
              @if (r.bestScore > r.score) { Tu mejor nota sigue siendo {{ r.bestScore }}%. }
            </p>
            <div class="card-actions">
              @if (r.nextLessonId) {
                <ui-button [routerLink]="['/student/cursos/leccion', r.nextLessonId]">Siguiente lección</ui-button>
              }
              <ui-button variant="secondary" [routerLink]="['/student/cursos', l.courseId]">Ver el curso</ui-button>
            </div>
          </div>
        } @else if (!allAtOnce() && !lastStep()) {
          <div class="panel" style="display: grid; gap: 0.6rem">
            @if (pendingInStep() > 0) {
              <p class="muted" style="margin: 0">Responde la actividad de este paso para continuar.</p>
            }
            <div class="player-nav">
              <ui-button variant="ghost" [disabled]="step() === 0" (click)="go(-1)">← Atrás</ui-button>
              <ui-button [disabled]="pendingInStep() > 0" (click)="go(1)">Continuar →</ui-button>
            </div>
          </div>
        } @else {
          <div class="panel" style="display: grid; gap: 0.6rem">
            @if (l.quizCount) {
              <p class="muted" style="margin: 0">
                Respondiste {{ answeredCount() }} de {{ l.quizCount }} actividades.
                @if (answeredCount() < l.quizCount) { Puedes terminar igual, pero las actividades sin responder cuentan como incorrectas. }
              </p>
            }
            <div class="player-nav">
              @if (!allAtOnce() && step() > 0) {
                <ui-button variant="ghost" (click)="go(-1)">← Atrás</ui-button>
              } @else if (l.previousLessonId) {
                <ui-button variant="ghost" [routerLink]="['/student/cursos/leccion', l.previousLessonId]">← Lección anterior</ui-button>
              } @else {
                <span></span>
              }
              <ui-button [loading]="saving()" (click)="complete()">Terminar lección</ui-button>
            </div>
          </div>
        }
      }
      <play-badges-toast [badges]="newBadges()" (closed)="newBadges.set([])" />
    </section>
  `
})
export class StudentLessonPage implements OnInit {
  private readonly api = inject(StudentCoursesApi);
  private readonly route = inject(ActivatedRoute);
  private readonly fx = inject(PlayFxService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly lesson = signal<StudentLesson | null>(null);
  readonly result = signal<LessonCompleteResult | null>(null);
  readonly newBadges = signal<Badge[]>([]);
  readonly answeredCount = signal(0);
  readonly allAtOnce = signal(false);
  readonly step = signal(0);
  readonly steps = computed(() => splitSteps(this.lesson()?.content ?? []));
  readonly lastStep = computed(() => this.step() >= this.steps().length - 1);
  private readonly answeredSet = signal<Set<number>>(new Set());
  private readonly feedback = signal<boolean | null>(null);
  readonly pendingInStep = computed(() => {
    const blocks = this.lesson()?.content ?? [];
    const done = this.answeredSet();
    return (this.steps()[this.step()] ?? []).filter((i) => GRADED.has(blocks[i]?.type) && !done.has(i)).length;
  });
  readonly guideLine = computed(() => {
    const l = this.lesson();
    if (!l) return '';
    const fb = this.feedback();
    if (fb !== null && this.pendingInStep() === 0) {
      return fb ? '¡Excelente! Lo hiciste muy bien. Continúa cuando quieras.' : 'No pasa nada: lee la explicación con calma y sigue adelante.';
    }
    if (this.step() === 0) {
      return `¡Hola! Soy tu guía de Luz Verde. Vamos paso a paso con «${l.title}». Lee, mira y responde: yo te acompaño.`;
    }
    const types = new Set((this.steps()[this.step()] ?? []).map((i) => l.content[i]?.type));
    if ([...types].some((t) => GRADED.has(t))) return '¡Tu turno! Responde la actividad para seguir avanzando.';
    if (types.has('video')) return 'Mira el video con atención; luego seguimos.';
    if (types.has('signs')) return 'Observa bien estas señales: las vas a encontrar en la vía.';
    if (types.has('flipcards')) return 'Toca cada tarjeta para descubrir lo que hay detrás.';
    if (types.has('match')) return 'Une cada elemento con su pareja.';
    if (types.has('hotspot')) return 'Explora la imagen y encuentra los puntos importantes.';
    if (this.lastStep()) return '¡Último paso! Al terminar, pulsa «Terminar lección».';
    return 'Lee con calma. Cuando estés listo, continúa.';
  });
  private answers: Record<number, ActivityAnswer> = {};

  go(delta: number): void {
    const next = Math.min(Math.max(this.step() + delta, 0), this.steps().length - 1);
    if (next === this.step()) return;
    this.step.set(next);
    this.feedback.set(null);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  readonly checker: QuizChecker = (blockIndex, answer) => this.api.check(this.lesson()!.id, blockIndex, answer);

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      this.load(Number(params.get('lessonId')));
    });
  }

  onAnswered(e: ActivityAnswered): void {
    this.answers[e.blockIndex] = e.value;
    this.answeredCount.set(Object.keys(this.answers).length);
    this.answeredSet.update((s) => new Set(s).add(e.blockIndex));
    this.feedback.set(e.correct);
    this.fx.play(e.correct ? 'correct' : 'wrong');
  }

  complete(): void {
    const l = this.lesson();
    if (!l || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    this.api.complete(l.id, this.answers).subscribe({
      next: (res) => {
        this.saving.set(false);
        this.result.set(res.result);
        this.fx.play('win');
        if (res.result.courseCompleted) this.fx.confetti(140);
        if (res.newBadges.length) {
          this.newBadges.set(res.newBadges);
          this.fx.play('badge');
        }
        window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  private load(lessonId: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.result.set(null);
    this.answers = {};
    this.answeredCount.set(0);
    this.answeredSet.set(new Set());
    this.feedback.set(null);
    this.step.set(0);
    this.allAtOnce.set(false);
    this.api.lesson(lessonId).subscribe({
      next: (l) => {
        this.lesson.set(l);
        this.loading.set(false);
        window.scrollTo({ top: 0 });
      },
      error: (err) => {
        this.error.set(mapApiError(err));
        this.loading.set(false);
      }
    });
  }
}
