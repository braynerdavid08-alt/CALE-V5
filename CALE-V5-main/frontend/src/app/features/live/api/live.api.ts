import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { env } from '../../../core/config/env';
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

  join(code: string, displayName: string) {
    return this.http.post<JoinLiveSessionResponse>(`${this.base}/sessions/join`, {
      code,
      displayName
    });
  }

  control(sessionId: number, action: string) {
    return this.http.post<LiveLobbyDto>(`${this.base}/sessions/${sessionId}/control`, {
      action
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
    const builder = new HubConnectionBuilder()
      .withUrl(this.hubUrl(), asHost && token
        ? { accessTokenFactory: () => token }
        : {})
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning);
    return builder.build();
  }

  qrImageUrl(joinUrl: string): string {
    return `https://api.qrserver.com/v1/create-qr-code/?size=240x240&data=${encodeURIComponent(joinUrl)}`;
  }
}
