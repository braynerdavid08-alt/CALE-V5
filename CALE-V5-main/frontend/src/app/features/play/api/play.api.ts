import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { env } from '../../../core/config/env';

export interface PlayOption {
  id: number;
  text: string;
  imageUrl?: string | null;
}

export interface PlayQuestion {
  id: number;
  text: string;
  type: string;
  imageUrl?: string | null;
  topic?: string | null;
  options: PlayOption[];
}

export interface AnsweredQuestion {
  questionId: number;
  optionId?: number | null;
  correct: boolean;
  correctOptionId?: number | null;
  explanation?: string | null;
}

export interface Streak {
  current: number;
  best: number;
  activeToday: boolean;
}

export interface Badge {
  code: string;
  title: string;
  description: string;
  icon: string;
  earned: boolean;
  earnedAt?: string | null;
  current: number;
  target: number;
}

export interface Level {
  level: number;
  name: string;
  xp: number;
  levelStartXp: number;
  nextLevelXp?: number | null;
  nextLevelName?: string | null;
  progressPercent: number;
}

export interface DailyChallenge {
  date: string;
  questions: PlayQuestion[];
  answers: AnsweredQuestion[];
  correctCount: number;
  completed: boolean;
  streak: Streak;
}

export interface AnswerFeedback {
  correct: boolean;
  correctOptionId?: number | null;
  explanation?: string | null;
}

export interface DailyAnswerResult extends AnswerFeedback {
  completed: boolean;
  correctCount: number;
  total: number;
  streak: Streak;
  newBadges: Badge[];
}

export interface Mistakes {
  dueCount: number;
  pendingCount: number;
  masteredCount: number;
  nextDueAt?: string | null;
  questions: PlayQuestion[];
}

export interface MistakeAnswerResult extends AnswerFeedback {
  mastered: boolean;
  box: number;
  newBadges: Badge[];
}

export interface ReadinessTopic {
  blockId: number;
  name: string;
  answered: number;
  correct: number;
  percent: number;
  level: 'alto' | 'medio' | 'bajo' | 'sin_datos';
  lowData: boolean;
}

export interface Readiness {
  overall: number;
  label: string;
  recommendation: string;
  recentAttemptsAverage: number;
  answeredQuestions: number;
  topics: ReadinessTopic[];
}

export interface Achievements {
  level: Level;
  badges: Badge[];
  newBadges: Badge[];
}

export interface Sign {
  code: string;
  family: string;
  name: string;
  imageUrl: string;
}

export interface QuickCheck {
  correct: boolean;
  correctOptionId?: number | null;
}

export interface GameSaved {
  score: number;
  best: number;
  isRecord: boolean;
  newBadges: Badge[];
}

export interface RankingEntry {
  position: number;
  userId: number;
  displayName: string;
  xp: number;
  isMe: boolean;
}

export interface RankingScope {
  scope: string;
  label: string;
  groupId?: number | null;
}

export interface Ranking {
  scope: string;
  scopeLabel: string;
  groupId?: number | null;
  weekStart: string;
  weekEnd: string;
  entries: RankingEntry[];
  myPosition?: number | null;
  myXp: number;
  participants: number;
  showInRanking: boolean;
  scopes: RankingScope[];
}

export interface PlaySummary {
  firstName: string;
  streak: Streak;
  level: Level;
  dailyAnswered: number;
  dailyTotal: number;
  dailyCompleted: boolean;
  mistakesDue: number;
  mistakesPending: number;
  readiness: number;
  readinessLabel: string;
  weakestTopic?: string | null;
  weeklyRank?: number | null;
  weeklyXp: number;
  earnedBadges: number;
  totalBadges: number;
  lastPercent?: number | null;
  lastPassed?: boolean | null;
  newBadges: Badge[];
}

export interface DuelPlayer {
  userId: number;
  name: string;
  answered: number;
  correct: number;
  finished: boolean;
  timeSeconds?: number | null;
}

export interface DuelState {
  code: string;
  status: 'waiting' | 'playing' | 'finished' | 'cancelled';
  me: DuelPlayer;
  opponent?: DuelPlayer | null;
  questions: PlayQuestion[];
  myAnswers: AnsweredQuestion[];
  result?: 'won' | 'lost' | 'draw' | null;
  timeLimitSeconds: number;
  secondsLeft?: number | null;
  newBadges: Badge[];
}

export interface DuelAnswerResult extends AnswerFeedback {
  state: DuelState;
}

export interface InactiveStudent {
  userId: number;
  name: string;
  email: string;
  lastActivityAt?: string | null;
  daysInactive?: number | null;
}

export interface SignsExamReport {
  examId: number;
  name: string;
  adminOwned: boolean;
  questions: number;
  inGame: number;
  notOfficial: number;
}

export interface SignsIssue {
  questionId: number;
  text: string;
  examName: string;
  reason: string;
}

export interface SignImageReport {
  exams: string[];
  matched: { code: string; name: string; imageUrl: string; questionId: number; answer: string }[];
  unmatched: { questionId: number; examName: string; answer: string; imageUrl: string }[];
  missingCodes: string[];
}

export interface SignsReport {
  inGame: number;
  candidates: number;
  exams: SignsExamReport[];
  issues: SignsIssue[];
}

@Injectable({ providedIn: 'root' })
export class PlayApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/student/play`;

  summary() {
    return this.http.get<PlaySummary>(`${this.base}/summary`);
  }

  daily() {
    return this.http.get<DailyChallenge>(`${this.base}/daily`);
  }

  answerDaily(questionId: number, optionId: number) {
    return this.http.post<DailyAnswerResult>(`${this.base}/daily/answer`, { questionId, optionId });
  }

  mistakes() {
    return this.http.get<Mistakes>(`${this.base}/mistakes`);
  }

  answerMistake(questionId: number, optionId: number) {
    return this.http.post<MistakeAnswerResult>(`${this.base}/mistakes/answer`, { questionId, optionId });
  }

  readiness() {
    return this.http.get<Readiness>(`${this.base}/readiness`);
  }

  achievements() {
    return this.http.get<Achievements>(`${this.base}/achievements`);
  }

  signs() {
    return this.http.get<Sign[]>(`${this.base}/signs`);
  }

  signsQuestions() {
    return this.http.get<PlayQuestion[]>(`${this.base}/signs/questions`);
  }

  signsReport() {
    return this.http.get<SignsReport>(`${env.apiUrl}/api/admin/play/signs-report`);
  }

  signImages(refresh = false) {
    return this.http.get<SignImageReport>(`${env.apiUrl}/api/admin/signal-images`, { params: { refresh } });
  }

  checkSignsQuestion(questionId: number, optionId: number) {
    return this.http.post<QuickCheck>(`${this.base}/signs/check`, { questionId, optionId });
  }

  saveSigns(correct: number, total: number) {
    return this.http.post<GameSaved>(`${this.base}/signs/result`, { correct, total });
  }

  ranking(scope?: string | null, groupId?: number | null) {
    let params = new HttpParams();
    if (scope) params = params.set('scope', scope);
    if (groupId != null) params = params.set('groupId', groupId);
    return this.http.get<Ranking>(`${this.base}/ranking`, { params });
  }

  setRankingVisibility(showInRanking: boolean) {
    return this.http.put<void>(`${this.base}/ranking/visibility`, { showInRanking });
  }

  createDuel() {
    return this.http.post<DuelState>(`${this.base}/duel`, {});
  }

  joinDuel(code: string) {
    return this.http.post<DuelState>(`${this.base}/duel/join`, { code });
  }

  duel(code: string) {
    return this.http.get<DuelState>(`${this.base}/duel/${encodeURIComponent(code)}`);
  }

  answerDuel(code: string, questionId: number, optionId: number) {
    return this.http.post<DuelAnswerResult>(
      `${this.base}/duel/${encodeURIComponent(code)}/answer`,
      { questionId, optionId }
    );
  }

  cancelDuel(code: string) {
    return this.http.delete<void>(`${this.base}/duel/${encodeURIComponent(code)}`);
  }
}

@Injectable({ providedIn: 'root' })
export class InactiveStudentsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/staff/inactive-students`;

  list(days = 7) {
    return this.http.get<InactiveStudent[]>(this.base, { params: { days } });
  }

  remind(userIds: number[]) {
    return this.http.post<{ sent: number }>(`${this.base}/remind`, { userIds });
  }
}
