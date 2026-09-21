import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { env } from '../../../core/config/env';
import { SessionStore } from '../../../core/auth/session.store';

export interface GameShowAnswerInput {
  text: string;
  points: number;
  aliases?: string[];
}

export interface GameShowRoundInput {
  questionText: string;
  sourceQuestionId?: number | null;
  answers: GameShowAnswerInput[];
}

export interface CreateGameShowBody {
  title: string;
  teamAName: string;
  teamBName: string;
  rounds: GameShowRoundInput[];
}

export interface GameShowBoardAnswerDto {
  id: number;
  rank: number;
  text: string | null;
  points: number | null;
  isRevealed: boolean;
}

export interface GameShowRoundDto {
  id: number;
  sortOrder: number;
  questionText: string;
  phase: string;
  controllingTeam: string | null;
  buzzWinnerTeam: string | null;
  strikes: number;
  roundPointsForController: number;
  stealSucceeded: boolean;
  answers: GameShowBoardAnswerDto[];
}

export interface GameShowPlayerDto {
  id: number;
  displayName: string;
  team: string;
  isConnected: boolean;
  userId?: number | null;
}

export interface GameShowLobbyDto {
  id: number;
  title: string;
  joinCode: string;
  status: string;
  teamAName: string;
  teamBName: string;
  teamAScore: number;
  teamBScore: number;
  currentRoundIndex: number;
  roundCount: number;
  players: GameShowPlayerDto[];
  currentRound: GameShowRoundDto | null;
  isHostView: boolean;
}

export interface JoinGameShowResultDto {
  sessionId: number;
  playerToken: string;
  lobby: GameShowLobbyDto;
}

export interface GameShowHistoryItemDto {
  id: number;
  title: string;
  status: string;
  teamAName: string;
  teamBName: string;
  teamAScore: number;
  teamBScore: number;
  createdAt: string;
  endedAt?: string | null;
  playerCount: number;
}

const TOKEN_KEY = 'cale.game-show.player';

@Injectable({ providedIn: 'root' })
export class GameShowApi {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionStore);
  private readonly base = env.apiUrl;

  create(body: CreateGameShowBody) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show`, body);
  }

  mine() {
    return this.http.get<GameShowHistoryItemDto[]>(`${this.base}/api/game-show/mine`);
  }

  get(id: number, playerToken?: string | null, host = false) {
    const params = new URLSearchParams();
    if (playerToken) params.set('playerToken', playerToken);
    if (host) params.set('host', 'true');
    const q = params.toString() ? `?${params}` : '';
    return this.http.get<GameShowLobbyDto>(`${this.base}/api/game-show/${id}${q}`);
  }

  join(code: string, displayName: string, team: string) {
    return this.http.post<JoinGameShowResultDto>(`${this.base}/api/game-show/join`, {
      code,
      displayName,
      team
    });
  }

  start(id: number) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${id}/start`, {});
  }

  pause(id: number) {
    return this.http.post(`${this.base}/api/game-show/${id}/pause`, {});
  }

  resume(id: number) {
    return this.http.post(`${this.base}/api/game-show/${id}/resume`, {});
  }

  buzz(id: number, playerToken: string) {
    return this.http.post(
      `${this.base}/api/game-show/${id}/buzz?playerToken=${encodeURIComponent(playerToken)}`,
      {}
    );
  }

  forceBuzz(id: number, team: string) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${id}/force-buzz`, { team });
  }

  answer(id: number, playerToken: string, text: string) {
    return this.http.post(
      `${this.base}/api/game-show/${id}/answer?playerToken=${encodeURIComponent(playerToken)}`,
      { text }
    );
  }

  reveal(id: number, answerId: number) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${id}/reveal/${answerId}`, {});
  }

  strike(id: number) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${id}/strike`, {});
  }

  assign(id: number, playerId: number, team: string) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${id}/assign`, {
      playerId,
      team
    });
  }

  nextRound(id: number) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${id}/next-round`, {});
  }

  finish(id: number) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${id}/finish`, {});
  }

  exportQuestions(id: number, format: 'csv' | 'json' = 'csv') {
    return this.http.get(`${this.base}/api/game-show/${id}/export?format=${format}`, {
      responseType: 'blob'
    });
  }

  /** Parse exported JSON/CSV into a create body (does not create a session). */
  importQuestions(file: File) {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<CreateGameShowBody>(`${this.base}/api/game-show/import`, form);
  }

  /** Parse and create a session from an exported JSON/CSV pack. */
  importAndCreate(file: File) {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/import/create`, form);
  }

  savePlayerToken(sessionId: number, token: string): void {
    localStorage.setItem(`${TOKEN_KEY}.${sessionId}`, token);
  }

  loadPlayerToken(sessionId: number): string | null {
    return localStorage.getItem(`${TOKEN_KEY}.${sessionId}`);
  }

  buildHub(): HubConnection {
    const url = `${this.base || ''}/hubs/game-show`;
    return new HubConnectionBuilder()
      .withUrl(url, {
        accessTokenFactory: () => this.session.token() || ''
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
  }
}
