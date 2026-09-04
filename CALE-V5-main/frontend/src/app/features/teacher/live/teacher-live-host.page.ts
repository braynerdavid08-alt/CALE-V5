import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiErrorComponent } from '../../../shared/ui/ui-error.component';
import { mapApiError } from '../../../core/http/map-api-error';
import {
  LiveAnalyticsDto,
  LiveAnswerRosterDto,
  LiveApi,
  LiveDoubtDto,
  LiveLobbyDto,
  LiveQuestionPayloadDto,
  LiveQuickQuestionRequest,
  LiveRankingDto,
  sanitizeLiveLobby
} from '../../live/api/live.api';
import { TeacherApi } from '../api/teacher.api';
import { computeSecondsLeft } from '../../live/live-timer.util';
import { PresentationApi } from '../presentations/presentation.api';
import {
  EditorSlide,
  dtoToEditorSlides,
  findAutoOpenQuestion,
  questionToLivePayload
} from '../presentations/presentation.models';

interface QuickOptionDraft {
  text: string;
}

@Component({
  selector: 'app-teacher-live-host-page',
  standalone: true,
  imports: [FormsModule, RouterLink, UiButtonComponent, UiErrorComponent],
  templateUrl: './teacher-live-host.page.html',
  styleUrl: './teacher-live-host.page.css'
})
export class TeacherLiveHostPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(LiveApi);
  private readonly teacherApi = inject(TeacherApi);
  private readonly presentationApi = inject(PresentationApi);
  private readonly sanitizer = inject(DomSanitizer);
  private hub: HubConnection | null = null;
  private timerId: ReturnType<typeof setInterval> | null = null;
  private timerQuestionId: number | null = null;
  private localDeadlineMs = 0;
  private autoCloseSent = false;
  private lastAnalyticsAtAnswers = -1;
  private exporting = false;
  private readonly firedSlideQuestionIds = new Set<string>();
  private openingSlideQuestion = false;
  private deckSlides: EditorSlide[] = [];
  private deckPresentationId: number | null = null;

  readonly lobby = signal<LiveLobbyDto | null>(null);
  readonly ranking = signal<LiveRankingDto | null>(null);
  readonly doubts = signal<LiveDoubtDto[]>([]);
  readonly analytics = signal<LiveAnalyticsDto | null>(null);
  readonly surpriseNotice = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly secondsLeft = signal<number | null>(null);
  readonly answersReceived = signal(0);
  readonly answerRoster = signal<LiveAnswerRosterDto | null>(null);
  readonly quickOpen = signal(false);
  readonly quickText = signal('');
  readonly quickExplanation = signal('');
  readonly quickTopic = signal('');
  readonly quickCorrectIndex = signal(0);
  readonly quickOptions = signal<QuickOptionDraft[]>([
    { text: '' },
    { text: '' }
  ]);
  readonly qrDataUrl = signal('');
  readonly presentationSlide = signal(0);
  readonly showPresentation = signal(false);
  readonly projectorMode = signal(false);
  readonly showMoreControls = signal(false);
  readonly presentationEmbedSrc = signal<SafeResourceUrl | null>(null);
  private lastEmbedPresentationId: number | null = null;
  private readonly bankNames = signal<Record<number, string>>({});
  private readonly onEmbedReady = (ev: MessageEvent): void => {
    if (ev.origin !== window.location.origin) {
      return;
    }
    if (ev.data?.type === 'cale-presentation-ready') {
      this.postSlideToEmbed(this.presentationSlide());
    }
  };

  ngOnInit(): void {
    window.addEventListener('message', this.onEmbedReady);
    this.teacherApi.banks(true, false).subscribe({
      next: (banks) => {
        const map: Record<number, string> = {};
        for (const b of banks) {
          map[b.id] = b.name;
        }
        this.bankNames.set(map);
      }
    });
    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('sessionId'));
      if (!id || Number.isNaN(id)) {
        return;
      }
      void this.hub?.stop();
      this.hub = null;
      this.analytics.set(null);
      this.surpriseNotice.set(null);
      this.answerRoster.set(null);
      this.autoCloseSent = false;
      this.lastAnalyticsAtAnswers = -1;
      this.reload(id);
      this.connectHub(id);
      this.loadDoubts(id);
      if (this.route.snapshot.queryParamMap.get('openDeck') === '1') {
        this.showPresentation.set(true);
      }
    });
  }

  ngOnDestroy(): void {
    window.removeEventListener('message', this.onEmbedReady);
    if (this.timerId) {
      clearInterval(this.timerId);
    }
    void this.hub?.stop();
  }

  qrUrl(): string {
    return this.qrDataUrl();
  }

  private refreshQr(joinUrl: string | null | undefined): void {
    if (!joinUrl) {
      this.qrDataUrl.set('');
      return;
    }

    void this.api.buildQrDataUrl(joinUrl).then((url) => this.qrDataUrl.set(url));
  }

  showRanking(): boolean {
    const l = this.lobby();
    if (!l) {
      return false;
    }
    return !!(l.config?.showRanking || l.mode === 'Competitive' || l.status === 'Ended');
  }

  configSummaryLines(): string[] {
    const l = this.lobby();
    if (!l?.config) {
      return [];
    }
    const names = this.bankNames();
    const bankIds = l.config.bankIds?.length
      ? l.config.bankIds
      : l.bankId
        ? [l.bankId]
        : [];
    const lines: string[] = [];
    if (bankIds.length) {
      lines.push(
        `Bancos: ${bankIds.map((id) => names[id] ?? `Banco ${id}`).join(', ')}`
      );
    }
    lines.push(`${l.config.questionCount} preguntas · ${l.config.secondsPerQuestion}s c/u`);
    const themed = Object.values(l.config.bankTopicFilters ?? {}).filter((t) => t?.length).length;
    if (themed) {
      lines.push(`Temas filtrados en ${themed} banco(s)`);
    }
    if (l.config.difficultyFilters?.length) {
      lines.push(`Dificultad: ${l.config.difficultyFilters.join(', ')}`);
    }
    const quotas = l.config.bankQuestionQuotas ?? {};
    const quotaEntries = Object.entries(quotas).filter(([, n]) => (n ?? 0) > 0);
    if (quotaEntries.length) {
      lines.push(
        `Cupos: ${quotaEntries
          .map(([id, n]) => `${names[Number(id)] ?? id}: ${n}`)
          .join(' · ')}`
      );
    }
    if (l.config.presentationId) {
      lines.push(`Presentación #${l.config.presentationId} vinculada`);
    }
    return lines;
  }

  linkedPresentationId(): number | null {
    const id = this.lobby()?.config?.presentationId;
    return id && id > 0 ? id : null;
  }

  openLinkedPresentation(): void {
    const id = this.linkedPresentationId();
    if (!id) {
      return;
    }
    this.showPresentation.set(true);
    this.syncPresentationEmbedSrc();
    this.ensureDeckLoaded(id);
  }

  openPresentClicker(): void {
    const presentationId = this.linkedPresentationId();
    const sessionId = this.lobby()?.sessionId;
    if (!presentationId || !sessionId) {
      return;
    }
    const url = this.router.serializeUrl(
      this.router.createUrlTree(['/teacher/presentations', presentationId, 'present'], {
        queryParams: {
          liveSessionId: sessionId,
          slide: this.presentationSlide()
        }
      })
    );
    window.open(url, '_blank', 'noopener');
  }

  /** Stable iframe URL — never include slide index (that reloads and flickers). */
  private syncPresentationEmbedSrc(): void {
    const id = this.linkedPresentationId();
    if (!id || !this.showPresentation()) {
      return;
    }
    if (this.lastEmbedPresentationId === id && this.presentationEmbedSrc()) {
      return;
    }
    this.lastEmbedPresentationId = id;
    const path = `/teacher/presentations/${id}/present?embed=1`;
    this.presentationEmbedSrc.set(this.sanitizer.bypassSecurityTrustResourceUrl(path));
  }

  onPresentationFrameLoad(): void {
    this.postSlideToEmbed(this.presentationSlide());
    // Angular inside the iframe may still be bootstrapping when load fires.
    window.setTimeout(() => this.postSlideToEmbed(this.presentationSlide()), 500);
    window.setTimeout(() => this.postSlideToEmbed(this.presentationSlide()), 1500);
  }

  private postSlideToEmbed(slideIndex: number): void {
    const iframe = document.getElementById('live-pres-iframe') as HTMLIFrameElement | null;
    iframe?.contentWindow?.postMessage(
      { type: 'cale-presentation-slide', slideIndex },
      window.location.origin
    );
  }

  async changePresentationSlide(delta: number): Promise<void> {
    const max = Math.max(0, this.deckSlides.length - 1);
    const next = Math.min(max, Math.max(0, this.presentationSlide() + delta));
    if (next === this.presentationSlide() && delta !== 0 && this.deckSlides.length > 0) {
      return;
    }
    this.presentationSlide.set(next);
    this.postSlideToEmbed(next);
    const sessionId = this.lobby()?.sessionId;
    if (sessionId && this.hub) {
      try {
        await this.hub.invoke('SyncPresentationSlide', sessionId, next);
      } catch {
        /* non-fatal */
      }
    }
    this.maybeOpenSlideQuestion(next);
  }

  private ensureDeckLoaded(presentationId: number): void {
    if (this.deckPresentationId === presentationId && this.deckSlides.length) {
      if (this.showPresentation()) {
        this.maybeOpenSlideQuestion(this.presentationSlide());
      }
      return;
    }
    this.presentationApi.get(presentationId).subscribe({
      next: (detail) => {
        this.deckPresentationId = presentationId;
        this.deckSlides = dtoToEditorSlides(detail);
        this.loadFiredSlideQuestions(this.lobby()?.sessionId);
        const max = Math.max(0, this.deckSlides.length - 1);
        if (this.presentationSlide() > max) {
          this.presentationSlide.set(max);
        }
        if (this.showPresentation() || this.route.snapshot.queryParamMap.get('openDeck') === '1') {
          this.maybeOpenSlideQuestion(this.presentationSlide());
        }
      },
      error: () => {
        this.deckSlides = [];
        this.deckPresentationId = null;
      }
    });
  }

  private maybeOpenSlideQuestion(slideIndex: number): void {
    const sessionId = this.lobby()?.sessionId;
    if (!sessionId || this.openingSlideQuestion) {
      return;
    }
    this.loadFiredSlideQuestions(sessionId);
    const slide = this.deckSlides[slideIndex];
    const el = findAutoOpenQuestion(slide);
    if (!el || this.firedSlideQuestionIds.has(el.id)) {
      return;
    }
    const payload = questionToLivePayload(el);
    if (!payload) {
      return;
    }
    this.openingSlideQuestion = true;
    this.firedSlideQuestionIds.add(el.id);
    this.persistFiredSlideQuestions(sessionId);
    this.api.control(sessionId, 'quick', payload).subscribe({
      next: (lobby) => {
        this.openingSlideQuestion = false;
        this.applyLobby(lobby);
      },
      error: () => {
        this.openingSlideQuestion = false;
        this.firedSlideQuestionIds.delete(el.id);
        this.persistFiredSlideQuestions(sessionId);
      }
    });
  }

  private loadFiredSlideQuestions(sessionId: number | undefined): void {
    if (!sessionId) {
      return;
    }
    try {
      const raw = localStorage.getItem(`cale-live-q-fired-${sessionId}`);
      const ids = raw ? (JSON.parse(raw) as string[]) : [];
      for (const id of ids) {
        this.firedSlideQuestionIds.add(id);
      }
    } catch {
      /* ignore */
    }
  }

  private persistFiredSlideQuestions(sessionId: number): void {
    try {
      localStorage.setItem(
        `cale-live-q-fired-${sessionId}`,
        JSON.stringify([...this.firedSlideQuestionIds])
      );
    } catch {
      /* ignore */
    }
  }

  duplicateConfig(): void {
    void this.router.navigate(['/teacher/live']);
  }

  control(action: string, quickQuestion?: LiveQuickQuestionRequest): void {
    const id = this.lobby()?.sessionId;
    if (!id) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.api.control(id, action, quickQuestion).subscribe({
      next: (lobby) => {
        this.loading.set(false);
        this.applyLobby(lobby);
        if (action === 'end' || lobby.status === 'Ended') {
          this.loadAnalytics(id);
        }
        if (action === 'quick') {
          this.resetQuickForm();
          this.quickOpen.set(false);
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  toggleQuick(): void {
    this.quickOpen.update((v) => !v);
  }

  applyQuickPreset(kind: 'vf' | 'abcd'): void {
    this.quickOpen.set(true);
    if (kind === 'vf') {
      this.quickOptions.set([{ text: 'Verdadero' }, { text: 'Falso' }]);
      this.quickCorrectIndex.set(0);
      return;
    }
    this.quickOptions.set([
      { text: 'A' },
      { text: 'B' },
      { text: 'C' },
      { text: 'D' }
    ]);
    this.quickCorrectIndex.set(0);
  }

  addQuickOption(): void {
    if (this.quickOptions().length >= 4) {
      return;
    }
    this.quickOptions.update((opts) => [...opts, { text: '' }]);
  }

  removeQuickOption(index: number): void {
    if (this.quickOptions().length <= 2) {
      return;
    }
    this.quickOptions.update((opts) => opts.filter((_, i) => i !== index));
    if (this.quickCorrectIndex() >= this.quickOptions().length) {
      this.quickCorrectIndex.set(0);
    }
  }

  setQuickOptionText(index: number, value: string): void {
    this.quickOptions.update((opts) =>
      opts.map((o, i) => (i === index ? { text: value } : o))
    );
  }

  submitQuick(): void {
    const text = this.quickText().trim();
    const options = this.quickOptions().map((o) => o.text.trim());
    if (!text) {
      this.error.set('Escribe el enunciado de la pregunta rápida.');
      return;
    }
    if (options.some((o) => !o) || options.length < 2) {
      this.error.set('Completa al menos 2 opciones.');
      return;
    }
    const correct = this.quickCorrectIndex();
    const payload: LiveQuickQuestionRequest = {
      text,
      options: options.map((optText, i) => ({
        text: optText,
        isCorrect: i === correct
      })),
      explanation: this.quickExplanation().trim() || null,
      topic: this.quickTopic().trim() || null
    };
    this.control('quick', payload);
  }

  rematch(): void {
    const id = this.lobby()?.sessionId;
    if (!id) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.api.rematch(id).subscribe({
      next: (res) => {
        this.loading.set(false);
        void this.router.navigate(['/teacher/live', res.newSessionId, 'host']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  exportCsv(): void {
    const id = this.lobby()?.sessionId;
    if (!id || this.exporting) {
      return;
    }
    this.exporting = true;
    this.error.set(null);
    this.api.exportResults(id).subscribe({
      next: (blob) => {
        this.exporting = false;
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `cale-live-${id}.csv`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => {
        this.exporting = false;
        this.error.set(mapApiError(err));
      }
    });
  }

  resolveDoubt(id: number): void {
    const sessionId = this.lobby()?.sessionId;
    if (!sessionId) {
      return;
    }
    this.api.resolveDoubt(sessionId, id).subscribe({
      next: () => this.loadDoubts(sessionId),
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private resetQuickForm(): void {
    this.quickText.set('');
    this.quickExplanation.set('');
    this.quickTopic.set('');
    this.quickCorrectIndex.set(0);
    this.quickOptions.set([{ text: '' }, { text: '' }]);
  }

  private reload(id: number): void {
    this.api.getHost(id).subscribe({
      next: (lobby) => {
        this.applyLobby(lobby);
        if (lobby.status === 'Ended' || lobby.status === 'Running' || lobby.status === 'Paused') {
          this.loadAnalytics(id, lobby.status !== 'Ended');
        }
      },
      error: (err) => this.error.set(mapApiError(err))
    });
  }

  private loadDoubts(id: number): void {
    this.api.listDoubts(id).subscribe({
      next: (rows) => this.doubts.set(rows),
      error: () => this.doubts.set([])
    });
  }

  private loadAnalytics(id: number, soft = false): void {
    this.api.analytics(id).subscribe({
      next: (a) => {
        this.analytics.set(a);
        this.lastAnalyticsAtAnswers = this.answersReceived();
      },
      error: () => {
        if (!soft) {
          this.analytics.set(null);
        }
      }
    });
  }

  private maybeRefreshMidAnalytics(): void {
    const l = this.lobby();
    if (!l || (l.status !== 'Running' && l.status !== 'Paused')) {
      return;
    }
    const n = this.answersReceived();
    if (n > 0 && (n % 3 === 0 || this.lastAnalyticsAtAnswers < 0)) {
      if (n === this.lastAnalyticsAtAnswers) {
        return;
      }
      this.loadAnalytics(l.sessionId, true);
    }
  }

  private applyLobby(lobby: LiveLobbyDto): void {
    const safe = lobby.revealCorrect ? lobby : sanitizeLiveLobby(lobby);
    this.lobby.set(safe);
    this.refreshQr(safe.joinUrl);
    this.answersReceived.set(lobby.answersReceived);
    if (lobby.ranking) {
      this.ranking.set(lobby.ranking);
    }
    if (lobby.config?.presentationId && this.route.snapshot.queryParamMap.get('openDeck') === '1') {
      this.showPresentation.set(true);
    }
    if (lobby.config?.presentationId) {
      this.ensureDeckLoaded(lobby.config.presentationId);
      this.loadFiredSlideQuestions(lobby.sessionId);
    }
    if (this.showPresentation()) {
      this.syncPresentationEmbedSrc();
    }
    this.syncTimer(lobby.currentQuestion ?? null);
  }

  toggleProjectorMode(): void {
    this.projectorMode.update((v) => !v);
  }

  private syncTimer(q: LiveQuestionPayloadDto | null): void {
    if (this.timerId) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
    if (!q) {
      this.secondsLeft.set(null);
      this.timerQuestionId = null;
      this.localDeadlineMs = 0;
      this.autoCloseSent = false;
      return;
    }

    const maxSecs = Math.max(5, q.secondsPerQuestion ?? this.lobby()?.config?.secondsPerQuestion ?? 30);
    const isNewQuestion = q.sessionQuestionId !== this.timerQuestionId;
    if (isNewQuestion) {
      this.timerQuestionId = q.sessionQuestionId;
      const synced = computeSecondsLeft(q, this.lobby()?.config?.secondsPerQuestion);
      const startLeft = synced ?? maxSecs;
      this.localDeadlineMs = Date.now() + startLeft * 1000;
    }

    const tick = () => {
      if (this.lobby()?.status === 'Paused') {
        return;
      }

      const synced = computeSecondsLeft(q, this.lobby()?.config?.secondsPerQuestion);
      const left = synced ?? Math.max(0, Math.ceil((this.localDeadlineMs - Date.now()) / 1000));
      this.secondsLeft.set(left);
      if (left === 0 && !this.autoCloseSent) {
        this.autoCloseSent = true;
        if (this.lobby()?.status === 'Running') {
          this.control('close');
        }
      }
    };

    tick();
    this.timerId = setInterval(tick, 250);
  }

  private connectHub(sessionId: number): void {
    this.hub = this.api.buildHub(true);
    this.hub.on('LobbyUpdated', (payload: LiveLobbyDto) => {
      const prev = this.lobby();
      if (prev?.revealCorrect && prev.currentQuestion && payload.currentQuestion
          && prev.currentQuestion.sessionQuestionId === payload.currentQuestion.sessionQuestionId) {
        this.applyLobby({
          ...payload,
          revealCorrect: true,
          currentQuestion: {
            ...payload.currentQuestion,
            options: prev.currentQuestion.options,
            explanation: prev.currentQuestion.explanation
          }
        });
        return;
      }
      this.applyLobby(payload);
    });
    this.hub.on('QuestionStarted', (payload: LiveQuestionPayloadDto) => {
      const current = this.lobby();
      if (current) {
        this.applyLobby({
          ...current,
          status: 'Running',
          revealCorrect: false,
          currentQuestion: payload,
          currentQuestionIndex: payload.index,
          questionCount: payload.total,
          answersReceived: 0
        });
      }
      this.answersReceived.set(0);
      this.answerRoster.set(null);
      this.autoCloseSent = false;
      this.surpriseNotice.set(payload.isSurprise ? '¡Pregunta sorpresa!' : null);
      this.syncTimer(payload);
    });
    this.hub.on('QuestionClosed', () => {
      this.autoCloseSent = true;
      const current = this.lobby();
      if (current?.currentQuestion) {
        this.syncTimer({ ...current.currentQuestion, closesAt: new Date().toISOString() });
      }
      this.secondsLeft.set(0);
    });
    this.hub.on('AnswerReceived', (payload: { answersReceived: number }) => {
      this.answersReceived.set(payload.answersReceived ?? 0);
      this.maybeRefreshMidAnalytics();
    });
    this.hub.on('RevealUpdated', (payload: LiveQuestionPayloadDto) => {
      const current = this.lobby();
      if (!current) {
        return;
      }
      const prevQ = current.currentQuestion;
      const payloadHasKey = !!payload.options?.some((o) => o.isCorrect === true);
      const mergedOptions =
        payloadHasKey ||
        !prevQ ||
        prevQ.sessionQuestionId !== payload.sessionQuestionId
          ? payload.options
          : payload.options.map((o) => ({
              ...o,
              isCorrect: prevQ.options.find((p) => p.id === o.id)?.isCorrect ?? o.isCorrect
            }));
      this.applyLobby({
        ...current,
        currentQuestion: {
          ...payload,
          options: mergedOptions,
          explanation: payload.explanation ?? prevQ?.explanation ?? null
        },
        revealCorrect: true
      });
    });
    this.hub.on('AnswerRosterUpdated', (payload: LiveAnswerRosterDto) => {
      this.answerRoster.set(payload ?? null);
    });
    this.hub.on('RankingUpdated', (payload: LiveRankingDto) => this.ranking.set(payload));
    this.hub.on('DoubtsUpdated', (payload: LiveDoubtDto[]) => this.doubts.set(payload ?? []));
    this.hub.on('SurpriseQueued', (payload: { message?: string; questionCount?: number }) => {
      this.surpriseNotice.set(payload?.message || 'Pregunta sorpresa en cola');
      const current = this.lobby();
      if (current && payload?.questionCount) {
        this.lobby.set({ ...current, questionCount: payload.questionCount });
      }
    });
    this.hub.on('SessionEnded', () => {
      const current = this.lobby();
      if (current) {
        this.applyLobby({ ...current, status: 'Ended' });
        this.loadAnalytics(current.sessionId);
      }
    });
    void this.hub.start().then(() => this.hub!.invoke('JoinAsHost', sessionId));
  }

  participationPercent(): number {
    const l = this.lobby();
    if (!l || l.participantCount === 0) {
      return 0;
    }
    return Math.round((100 * this.answersReceived()) / l.participantCount);
  }

  optionLetter(index: number): string {
    return String.fromCharCode(65 + index);
  }
}
