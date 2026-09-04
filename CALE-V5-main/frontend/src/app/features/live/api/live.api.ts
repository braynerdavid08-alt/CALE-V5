import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { env } from '../../../core/config/env';
import { buildQrDataUrl as buildQrDataUrlCore } from '../../../core/qr/build-qr-data-url';
import { SessionStore } from '../../../core/auth/session.store';

export interface LiveSessionConfigDto {
  questionCount: number;
  secondsPerQuestion: number;
  randomize: boolean;
  shuffleOptions: boolean;
  showRanking: boolean;
  anonymousNames: boolean;
  feedbackTiming: string;
  topicFilter?: string | null;
  difficultyFilter?: string | null;
  caleStandardPreset: boolean;
  bankIds?: number[] | null;
  topicFilters?: string[] | null;
  bankTopicFilters?: Record<number, string[]> | null;
  bankQuestionQuotas?: Record<number, number> | null;
  difficultyFilters?: string[] | null;
  presentationId?: number | null;
}

export interface LiveOptionDto {
  id: number;
  text: string;
  imageUrl?: string | null;
  isCorrect?: boolean | null;
}

export interface LiveQuestionPayloadDto {
  sessionQuestionId: number;
  questionId: number;
  index: number;
  total: number;
  text: string;
  imageUrl?: string | null;
  topic?: string | null;
  explanation?: string | null;
  options: LiveOptionDto[];
  opensAt?: string | null;
  closesAt?: string | null;
  secondsPerQuestion: number;
  revealCorrect: boolean;
  isSurprise?: boolean;
}

export interface LiveParticipantDto {
  id: number;
  displayName: string;
  isConnected: boolean;
  userId?: number | null;
}

export interface LiveRankEntryDto {
  rank: number;
  participantId: number;
  displayName: string;
  score: number;
  correctCount: number;
  answerCount: number;
}

export interface LiveRankingDto {
  top: LiveRankEntryDto[];
  myParticipantId?: number | null;
  myRank?: number | null;
  myScore?: number | null;
}

export interface LiveLobbyDto {
  sessionId: number;
  title: string;
  joinCode: string;
  status: string;
  mode: string;
  bankId: number;
  participantCount: number;
  connectedCount: number;
  participants: LiveParticipantDto[];
  config: LiveSessionConfigDto;
  questionCount: number;
  currentQuestionIndex: number;
  revealCorrect: boolean;
  currentQuestion?: LiveQuestionPayloadDto | null;
  answersReceived: number;
  joinUrl: string;
  ranking?: LiveRankingDto | null;
}

export interface LivePresentationSlideDto {
  position: number;
  title: string;
  backgroundJson: string;
  elementsJson: string;
}

export interface LivePresentationDto {
  presentationId: number;
  title: string;
  slideIndex: number;
  slideCount: number;
  slides: LivePresentationSlideDto[];
}

export interface JoinLiveSessionResponse {
  sessionId: number;
  participantToken: string;
  participantId: number;
  displayName: string;
  title: string;
  status: string;
  joinCode: string;
}

export interface LiveDoubtDto {
  id: number;
  participantId: number;
  authorName: string;
  text: string;
  voteCount: number;
  isResolved: boolean;
  votedByMe: boolean;
  createdAt: string;
}

export interface LiveTopicStatDto {
  topic: string;
  answered: number;
  correct: number;
  accuracyPercent: number;
}

export interface LiveQuestionStatDto {
  index: number;
  sessionQuestionId: number;
  text: string;
  topic?: string | null;
  answered: number;
  correct: number;
  accuracyPercent: number;
  isSurprise: boolean;
}

export interface LiveAnalyticsDto {
  sessionId: number;
  title: string;
  mode: string;
  participantCount: number;
  questionCount: number;
  totalAnswers: number;
  correctAnswers: number;
  overallAccuracyPercent: number;
  questions: LiveQuestionStatDto[];
  topics: LiveTopicStatDto[];
  recommendations: string[];
  ranking: LiveRankingDto;
}

export interface LiveQuickOptionRequest {
  text: string;
  isCorrect: boolean;
}

export interface LiveQuickQuestionRequest {
  text: string;
  options: LiveQuickOptionRequest[];
  explanation?: string | null;
  topic?: string | null;
}

export interface LiveRematchResponse {
  newSessionId: number;
  joinCode: string;
  joinUrl: string;
  lobby: LiveLobbyDto;
}

@Injectable({ providedIn: 'root' })
export class LiveApi {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionStore);
  private readonly base = `${env.apiUrl}/api/live`;

  create(body: {
    title?: string;
    mode: string;
    bankId?: number | null;
    bankIds?: number[] | null;
    config?: Partial<LiveSessionConfigDto>;
  }) {
    return this.http.post<LiveLobbyDto>(`${this.base}/sessions`, body);
  }

  getHost(sessionId: number) {
    return this.http.get<LiveLobbyDto>(`${this.base}/sessions/${sessionId}`);
  }

  getPlay(sessionId: number, token: string) {
    return this.http.get<LiveLobbyDto>(`${this.base}/sessions/${sessionId}/play`, {
      params: { token }
    });
  }

  getPresentation(sessionId: number, token: string) {
    return this.http.get<LivePresentationDto>(`${this.base}/sessions/${sessionId}/presentation`, {
      params: { token }
    });
  }

  join(code: string, displayName: string) {
    return this.http.post<JoinLiveSessionResponse>(`${this.base}/sessions/join`, {
      code,
      displayName
    });
  }

  control(sessionId: number, action: string, quickQuestion?: LiveQuickQuestionRequest) {
    return this.http.post<LiveLobbyDto>(`${this.base}/sessions/${sessionId}/control`, {
      action,
      ...(quickQuestion ? { quickQuestion } : {})
    });
  }

  exportResults(sessionId: number) {
    return this.http.get(`${this.base}/sessions/${sessionId}/export`, {
      responseType: 'blob'
    });
  }

  answer(sessionId: number, sessionQuestionId: number, participantToken: string, optionId: number) {
    return this.http.post<{ ok: boolean; points?: number | null }>(
      `${this.base}/sessions/${sessionId}/questions/${sessionQuestionId}/answer`,
      { participantToken, optionId }
    );
  }

  listDoubts(sessionId: number, token?: string) {
    return this.http.get<LiveDoubtDto[]>(`${this.base}/sessions/${sessionId}/doubts`, {
      params: token ? { token } : {}
    });
  }

  postDoubt(sessionId: number, participantToken: string, text: string) {
    return this.http.post<LiveDoubtDto>(`${this.base}/sessions/${sessionId}/doubts`, {
      participantToken,
      text
    });
  }

  voteDoubt(sessionId: number, doubtId: number, participantToken: string) {
    return this.http.post<LiveDoubtDto>(
      `${this.base}/sessions/${sessionId}/doubts/${doubtId}/vote`,
      { participantToken }
    );
  }

  resolveDoubt(sessionId: number, doubtId: number) {
    return this.http.post<LiveDoubtDto>(
      `${this.base}/sessions/${sessionId}/doubts/${doubtId}/resolve`,
      {}
    );
  }

  analytics(sessionId: number) {
    return this.http.get<LiveAnalyticsDto>(`${this.base}/sessions/${sessionId}/analytics`);
  }

  rematch(sessionId: number) {
    return this.http.post<LiveRematchResponse>(`${this.base}/sessions/${sessionId}/rematch`, {});
  }

  hubUrl(): string {
    const root = env.apiUrl || (typeof location !== 'undefined' ? location.origin : '');
    return `${root}/hubs/live`;
  }

  buildHub(asHost: boolean): HubConnection {
    const token = this.session.token();
    const url = this.hubUrl();
    const options = token
      ? { accessTokenFactory: () => token, withCredentials: true }
      : { withCredentials: true };
    const builder = new HubConnectionBuilder()
      .withUrl(url, options)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning);
    return builder.build();
  }

  qrImageUrl(joinUrl: string): string {
    return '';
  }

  buildQrDataUrl(joinUrl: string): Promise<string> {
    return Promise.resolve(buildQrDataUrlCore(joinUrl, 240));
  }
}

/** Strip correct answers until the host reveals them (defense in depth vs API/SignalR). */
export function sanitizeLiveQuestion(q: LiveQuestionPayloadDto): LiveQuestionPayloadDto {
  if (q.revealCorrect) {
    return q;
  }
  return {
    ...q,
    explanation: null,
    revealCorrect: false,
    options: q.options.map((o) => ({ ...o, isCorrect: null }))
  };
}

export function sanitizeLiveLobby(lobby: LiveLobbyDto): LiveLobbyDto {
  if (!lobby.currentQuestion) {
    return lobby;
  }
  return {
    ...lobby,
    currentQuestion: sanitizeLiveQuestion(lobby.currentQuestion)
  };
}
