import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { SessionStore } from '../../../core/auth/session.store';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { mapApiError } from '../../../core/http/map-api-error';
import { UiBadgeComponent } from '../../../shared/ui/ui-badge.component';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiDialogComponent } from '../../../shared/ui/ui-dialog.component';
import { UiEmptyComponent } from '../../../shared/ui/ui-empty.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { UiLoadingComponent } from '../../../shared/ui/ui-loading.component';
import { UiPageHeaderComponent } from '../../../shared/ui/ui-page-header.component';
import { UiStatComponent } from '../../../shared/ui/ui-stat.component';
import { UiSuccessComponent } from '../../../shared/ui/ui-success.component';
import {
  BankDto,
  ExamApi,
  ExamDto,
  FinishResponse,
  ReviewResponse,
  StartExamResponse,
  TakeQuestionDto
} from '../api/exam.api';

/** Regla fija de CALE (ScoringRules.PassPercent). */
export const CALE_PASS_PERCENT = 80;

export interface PracticePreset {
  id: string;
  label: string;
  hint: string;
  questionCount: number;
  timeMinutes: number;
}

const PRESETS: PracticePreset[] = [
  {
    id: 'rapida',
    label: 'Rápida',
    hint: 'Calentamiento corto',
    questionCount: 10,
    timeMinutes: 15
  },
  {
    id: 'estandar',
    label: 'Estándar CALE',
    hint: 'Formato recomendado',
    questionCount: 25,
    timeMinutes: 30
  },
  {
    id: 'completa',
    label: 'Completa',
    hint: 'Sesión exigente',
    questionCount: 40,
    timeMinutes: 45
  },
  {
    id: 'visual',
    label: 'Reconocimiento visual',
    hint: 'Ideal para señales',
    questionCount: 20,
    timeMinutes: 25
  }
];

@Component({
  selector: 'app-simulator-page',
  standalone: true,
  imports: [
    FormsModule,
    UiBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    UiDialogComponent,
    UiEmptyComponent,
    UiErrorComponent,
    UiLoadingComponent,
    UiPageHeaderComponent,
    UiStatComponent,
    UiSuccessComponent
  ],
  templateUrl: './simulator.page.html',
  styleUrl: './simulator.page.css'
})
export class SimulatorPage implements OnInit, OnDestroy {
  private readonly api = inject(ExamApi);
  private readonly router = inject(Router);
  private readonly sessionStore = inject(SessionStore);

  readonly passPercent = CALE_PASS_PERCENT;
  readonly presets = PRESETS;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly banks = signal<BankDto[]>([]);
  readonly exams = signal<ExamDto[]>([]);
  readonly step = signal<'setup' | 'take' | 'result' | 'review'>('setup');
  readonly session = signal<StartExamResponse | null>(null);
  readonly current = signal(0);
  readonly selected = signal<number | null>(null);
  readonly result = signal<FinishResponse | null>(null);
  readonly review = signal<ReviewResponse | null>(null);
  readonly media = resolveMediaUrl;
  readonly remaining = signal<string | null>(null);
  readonly confirmExit = signal(false);
  readonly ok = signal<string | null>(null);
  readonly presetId = signal<string>('estandar');
  readonly customMode = signal(false);
  readonly answeredCount = signal(0);

  private timer: ReturnType<typeof setInterval> | null = null;
  private readonly answers: Record<number, number> = {};

  readonly bankId = signal<number | null>(null);
  questionCount = 25;
  timeMinutes = 30;
  stars = 5;
  comment = '';

  readonly selectedBank = computed(() => {
    const id = this.bankId();
    return this.banks().find((b) => b.id === id) ?? null;
  });

  readonly neededCorrect = computed(() =>
    Math.ceil((this.questionCount * this.passPercent) / 100)
  );

  readonly secondsPerQuestion = computed(() => {
    if (this.questionCount < 1) return 0;
    return Math.round((this.timeMinutes * 60) / this.questionCount);
  });

  ngOnDestroy(): void {
    this.clearTimer();
  }

  ngOnInit(): void {
    this.applyPreset('estandar');
    this.api.banks().subscribe({
      next: (banks) => {
        this.banks.set(banks);
        const preferred =
          banks.find((b) => /normas/i.test(b.name))
          ?? banks.find((b) => /señal|senal/i.test(b.name))
          ?? banks[0];
        if (preferred) {
          this.bankId.set(preferred.id);
          if (/señal|senal|visual/i.test(preferred.name)) {
            this.applyPreset('visual');
          }
        }
      },
      error: (err) => this.error.set(mapApiError(err))
    });
    this.api.published().subscribe({
      next: (exams) => {
        this.exams.set(exams);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  question(): TakeQuestionDto | null {
    return this.session()?.questions[this.current()] ?? null;
  }

  selectBank(id: number): void {
    this.bankId.set(id);
    const bank = this.banks().find((b) => b.id === id);
    if (bank && /señal|senal|visual/i.test(bank.name) && !this.customMode()) {
      this.applyPreset('visual');
    }
  }

  applyPreset(id: string): void {
    const preset = this.presets.find((p) => p.id === id);
    if (!preset) return;
    this.presetId.set(id);
    this.customMode.set(false);
    this.questionCount = preset.questionCount;
    this.timeMinutes = preset.timeMinutes;
  }

  enableCustom(): void {
    this.customMode.set(true);
    this.presetId.set('custom');
  }

  bankKind(bank: BankDto): string {
    if (/señal|senal|visual/i.test(bank.name)) return 'Reconocimiento visual';
    if (/norma/i.test(bank.name)) return 'Normas de tránsito';
    return 'Banco CALE';
  }

  startPractice(): void {
    if (!this.bankId()) {
      this.error.set('Elige un banco oficial para practicar.');
      return;
    }
    if (this.questionCount < 1 || this.timeMinutes < 1) {
      this.error.set('Preguntas y minutos deben ser al menos 1.');
      return;
    }
    const bank = this.selectedBank();
    if (bank && this.questionCount > bank.questionCount) {
      this.error.set(
        `Este banco solo tiene ${bank.questionCount} preguntas activas.`
      );
      return;
    }
    this.start({
      bankId: this.bankId(),
      examId: null,
      questionCount: this.questionCount,
      mode: 'practice',
      timeMinutes: this.timeMinutes
    });
  }

  startExam(exam: ExamDto): void {
    this.start({
      bankId: null,
      examId: exam.id,
      questionCount: exam.questionCount,
      mode: 'exam',
      timeMinutes: exam.timeMinutes
    });
  }

  examNeededCorrect(exam: ExamDto): number {
    return Math.ceil((exam.questionCount * this.passPercent) / 100);
  }

  answer(optionId: number): void {
    this.selected.set(optionId);
    const attemptId = this.session()?.attemptId;
    const question = this.question();
    if (!attemptId || !question) {
      return;
    }
    this.answers[question.id] = optionId;
    this.answeredCount.set(Object.keys(this.answers).length);
    this.api.answer(attemptId, question.id, optionId).subscribe({
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  progress(): number {
    const total = this.session()?.questions.length ?? 0;
    if (!total) {
      return 0;
    }
    return ((this.current() + 1) / total) * 100;
  }

  answerProgress(): number {
    const total = this.session()?.questions.length ?? 0;
    if (!total) return 0;
    return (this.answeredCount() / total) * 100;
  }

  prev(): void {
    if (this.current() === 0) {
      return;
    }
    this.current.set(this.current() - 1);
    this.restoreSelected();
  }

  next(): void {
    const total = this.session()?.questions.length ?? 0;
    if (this.current() + 1 < total) {
      this.current.set(this.current() + 1);
      this.restoreSelected();
      return;
    }
    this.finish();
  }

  finish(): void {
    const attemptId = this.session()?.attemptId;
    if (!attemptId) {
      return;
    }
    this.clearTimer();
    this.api.finish(attemptId).subscribe({
      next: (result) => {
        this.result.set(result);
        this.step.set('result');
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  askAbort(): void {
    this.confirmExit.set(true);
  }

  confirmAbort(): void {
    this.confirmExit.set(false);
    this.clearTimer();
    void this.router.navigateByUrl(this.sessionStore.homeRoute());
  }

  goHome(): void {
    void this.router.navigateByUrl(this.sessionStore.homeRoute());
  }

  backToSetup(): void {
    this.clearTimer();
    this.step.set('setup');
    this.session.set(null);
    this.result.set(null);
    this.review.set(null);
    this.ok.set(null);
  }

  openReview(): void {
    const attemptId = this.session()?.attemptId;
    if (!attemptId) {
      return;
    }
    this.api.review(attemptId).subscribe({
      next: (review) => {
        this.review.set(review);
        this.step.set('review');
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  rate(): void {
    const attemptId = this.session()?.attemptId;
    if (!attemptId) {
      return;
    }
    this.api.rate(attemptId, this.stars, this.comment).subscribe({
      next: () => {
        this.error.set(null);
        this.ok.set('Valoración enviada.');
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private start(body: {
    bankId?: number | null;
    examId?: number | null;
    questionCount: number;
    mode: string;
    timeMinutes: number;
  }): void {
    this.error.set(null);
    this.api.start(body).subscribe({
      next: (session) => {
        this.session.set(session);
        this.current.set(0);
        this.selected.set(null);
        Object.keys(this.answers).forEach((k) => delete this.answers[Number(k)]);
        this.answeredCount.set(0);
        this.step.set('take');
        this.startTimer(session.expiresAt);
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private startTimer(expiresAt?: string | null): void {
    this.clearTimer();
    if (!expiresAt) {
      this.remaining.set(null);
      return;
    }
    const end = new Date(expiresAt).getTime();
    const tick = () => {
      const diff = end - Date.now();
      if (diff <= 0) {
        this.clearTimer();
        this.remaining.set('0:00');
        this.finish();
        return;
      }
      const m = Math.floor(diff / 60000);
      const s = Math.floor((diff % 60000) / 1000);
      this.remaining.set(`${m}:${s.toString().padStart(2, '0')}`);
    };
    tick();
    this.timer = setInterval(tick, 1000);
  }

  formatTime(seconds: number): string {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m}:${s.toString().padStart(2, '0')}`;
  }

  private restoreSelected(): void {
    const q = this.question();
    this.selected.set(q ? this.answers[q.id] ?? null : null);
  }

  private clearTimer(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }
}
