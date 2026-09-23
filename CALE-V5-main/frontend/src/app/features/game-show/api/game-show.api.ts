import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { env } from '../../../core/config/env';
import { SessionStore } from '../../../core/auth/session.store';

export interface GameShowAnswerInput {
  text: string;
  points: number;
  aliases?: string[];
  isActive?: boolean;
}

export interface GameShowRoundInput {
  questionText: string;
  sourceQuestionId?: number | null;
  answers: GameShowAnswerInput[];
  category?: string | null;
  isActive?: boolean;
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
  answerDeadlineUtc: string | null;
  answers: GameShowBoardAnswerDto[];
  activePlayerId?: number | null;
  activePlayerName?: string | null;
  activePlayerAccent?: string | null;
}

export interface GameShowPlayerDto {
  id: number;
  displayName: string;
  team: string;
  isConnected: boolean;
  userId?: number | null;
  accentColor?: string | null;
}

export interface GameShowRoundChampionDto {
  playerId: number;
  displayName: string;
  team: string;
  correctAnswers: number;
}

export interface GameShowPlayerStandingDto {
  playerId: number;
  displayName: string;
  team: string;
  correctAnswers: number;
  stealsWon: number;
  buzzWins: number;
  accentColor: string;
}

export interface GameShowPackLeaderboardEntryDto {
  sessionId: number;
  title: string;
  teamAName: string;
  teamBName: string;
  teamAScore: number;
  teamBScore: number;
  combinedScore: number;
  endedAt?: string | null;
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
  viewerPlayerId?: number | null;
  viewerTeam?: string | null;
  lightningUntilUtc?: string | null;
  isLightning?: boolean;
  sourcePackId?: number | null;
  roundChampion?: GameShowRoundChampionDto | null;
  playerStandings?: GameShowPlayerStandingDto[] | null;
  packLeaderboard?: GameShowPackLeaderboardEntryDto[] | null;
  settings?: GameShowSettingsDto | null;
}

export interface GameShowSettingsDto {
  faceOffSeconds: number;
  controlSeconds: number;
  stealSeconds: number;
  lightningSeconds: number;
  roundTransitionSeconds: number;
  drumrollMs: number;
  revealHighlightMs: number;
  strikeFlashMs: number;
  celebrationMs: number;
  correctFlashMs: number;
  scoreboardFlashMs: number;
  maxStrikes: number;
  enableFaceOff: boolean;
  enableSteal: boolean;
  enableLightning: boolean;
  enableSounds: boolean;
  enableAnimations: boolean;
  allowPause: boolean;
  allowSkipRound: boolean;
  allowHostEndRound: boolean;
  enableAudienceVote: boolean;
  tieBreakMode: string;
  updatedAt?: string | null;
  updatedByUserId?: number | null;
}

export interface JoinGameShowResultDto {
  sessionId: number;
  playerToken: string;
  playerId: number;
  team: string;
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

export interface GameShowPackSummaryDto {
  id: number;
  name: string;
  notes?: string | null;
  roundCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface GameShowPackDetailDto {
  id: number;
  name: string;
  notes?: string | null;
  roundCount: number;
  createdAt: string;
  updatedAt: string;
  body: CreateGameShowBody;
}

export interface UpsertGameShowPackBody {
  name: string;
  notes?: string | null;
  rounds: GameShowRoundInput[];
  defaultTeamAName?: string | null;
  defaultTeamBName?: string | null;
}

export interface GameShowRoundStatDto {
  roundIndex: number;
  questionText: string;
  pointsAwarded: number;
  stealSucceeded: boolean;
  controllingTeam: string | null;
  strikes: number;
  correctAttempts: number;
  wrongAttempts: number;
}

export interface GameShowStatsDto {
  sessionId: number;
  title: string;
  status: string;
  teamAName: string;
  teamBName: string;
  teamAScore: number;
  teamBScore: number;
  winnerTeam: string | null;
  playerCount: number;
  roundCount: number;
  correctAnswers: number;
  wrongAnswers: number;
  stealsSucceeded: number;
  stealsFailed: number;
  rounds: GameShowRoundStatDto[];
  createdAt: string;
  endedAt?: string | null;
  players?: GameShowPlayerStandingDto[] | null;
  mvp?: GameShowPlayerStandingDto | null;
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

  listPacks() {
    return this.http.get<GameShowPackSummaryDto[]>(`${this.base}/api/game-show/packs`);
  }

  getPack(packId: number) {
    return this.http.get<GameShowPackDetailDto>(`${this.base}/api/game-show/packs/${packId}`);
  }

  savePack(body: UpsertGameShowPackBody) {
    return this.http.post<GameShowPackDetailDto>(`${this.base}/api/game-show/packs`, body);
  }

  updatePack(packId: number, body: UpsertGameShowPackBody) {
    return this.http.put<GameShowPackDetailDto>(`${this.base}/api/game-show/packs/${packId}`, body);
  }

  deletePack(packId: number) {
    return this.http.delete(`${this.base}/api/game-show/packs/${packId}`);
  }

  getGlobalSettings() {
    return this.http.get<GameShowSettingsDto>(`${this.base}/api/game-show/settings`);
  }

  putGlobalSettings(body: GameShowSettingsDto) {
    return this.http.put<GameShowSettingsDto>(`${this.base}/api/game-show/settings`, body);
  }

  restoreGlobalSettings() {
    return this.http.post<GameShowSettingsDto>(`${this.base}/api/game-show/settings/restore`, {});
  }

  getSessionSettings(sessionId: number) {
    return this.http.get<GameShowSettingsDto>(`${this.base}/api/game-show/${sessionId}/settings`);
  }

  putSessionSettings(sessionId: number, body: GameShowSettingsDto) {
    return this.http.put<GameShowSettingsDto>(`${this.base}/api/game-show/${sessionId}/settings`, body);
  }

  createFromPack(
    packId: number,
    body: { title?: string; teamAName?: string; teamBName?: string }
  ) {
    return this.http.post<GameShowLobbyDto>(
      `${this.base}/api/game-show/packs/${packId}/create-session`,
      body
    );
  }

  replay(sessionId: number, body: { title?: string; teamAName?: string; teamBName?: string } = {}) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${sessionId}/replay`, body);
  }

  stats(sessionId: number) {
    return this.http.get<GameShowStatsDto>(`${this.base}/api/game-show/${sessionId}/stats`);
  }

  sessionPack(sessionId: number) {
    return this.http.get<CreateGameShowBody>(`${this.base}/api/game-show/${sessionId}/pack`);
  }

  get(id: number, playerToken?: string | null, host = false) {
    const params = new URLSearchParams();
    if (playerToken) params.set('playerToken', playerToken);
    if (host) params.set('host', 'true');
    const q = params.toString() ? `?${params}` : '';
    return this.http.get<GameShowLobbyDto>(`${this.base}/api/game-show/${id}${q}`);
  }

  join(code: string, displayName: string, team: string = 'auto') {
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
    return this.http.post<GameShowLobbyDto>(
      `${this.base}/api/game-show/${id}/buzz?playerToken=${encodeURIComponent(playerToken)}`,
      {}
    );
  }

  forceBuzz(id: number, team: string) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${id}/force-buzz`, { team });
  }

  answer(id: number, playerToken: string, text: string) {
    return this.http.post<GameShowLobbyDto>(
      `${this.base}/api/game-show/${id}/answer?playerToken=${encodeURIComponent(playerToken)}`,
      { text }
    );
  }

  timeout(id: number, playerToken?: string | null) {
    const q = playerToken ? `?playerToken=${encodeURIComponent(playerToken)}` : '';
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${id}/timeout${q}`, {});
  }

  reveal(id: number, answerId: number) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${id}/reveal/${answerId}`, {});
  }

  strike(id: number) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${id}/strike`, {});
  }

  /** Cierra la oportunidad de robo sin acierto del equipo contrario. */
  failSteal(id: number) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${id}/fail-steal`, {});
  }

  /** Termina la ronda actual revelando lo que falte. */
  endRound(id: number) {
    return this.http.post<GameShowLobbyDto>(`${this.base}/api/game-show/${id}/end-round`, {});
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

  /** Pack oficial precargado (rondas listas para revisar y crear). */
  officialPack() {
    return this.http.get<CreateGameShowBody>(`${this.base}/api/game-show/packs/oficial`);
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
