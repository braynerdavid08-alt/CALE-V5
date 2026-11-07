import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { firstValueFrom } from 'rxjs';
import { SessionStore } from '../../../core/auth/session.store';
import { env } from '../../../core/config/env';
import { AuthApi } from '../../auth/api/auth.api';
import { AnsweredQuestion, Badge, Level, PlayQuestion } from './play.api';

export type TournamentStatus =
  | 'registration_open'
  | 'registration_closed'
  | 'preparing'
  | 'in_progress'
  | 'finished'
  | 'cancelled';

export type TournamentMatchStatus = 'pending' | 'active' | 'finished';

export type TournamentFinishReason = 'score' | 'time' | 'sudden_death' | 'walkover' | 'bye';

export interface CreateTournamentRequest {
  name: string;
  description?: string | null;
  /** ISO 8601 (UTC). */
  scheduledAt: string;
  /** 4 | 8 | 16 */
  capacity: number;
  /** 10..120 */
  secondsPerQuestion: number;
  /** 'random' | 'manual' */
  questionMode: string;
  categories?: string[] | null;
  manualQuestionIds?: number[] | null;
}

export interface TournamentListItem {
  id: number;
  name: string;
  description?: string | null;
  scheduledAt: string;
  status: TournamentStatus | string;
  capacity: number;
  registeredCount: number;
  connectedCount: number;
  organizerName: string;
  amIRegistered: boolean;
  myMatchId?: number | null;
}

export interface TournamentParticipant {
  userId: number;
  name: string;
  isConnected: boolean;
  isEliminated: boolean;
  bracketSlot?: number | null;
}

export interface TournamentLobby {
  id: number;
  name: string;
  description?: string | null;
  scheduledAt: string;
  status: TournamentStatus | string;
  capacity: number;
  bracketSize: number;
  currentRoundNumber: number;
  organizerName: string;
  organizerUserId: number;
  participants: TournamentParticipant[];
  amIRegistered: boolean;
  canJoin: boolean;
  myMatchId?: number | null;
  championUserId?: number | null;
  championName?: string | null;
}

export interface TournamentBracketMatch {
  id: number;
  roundNumber: number;
  roundName: string;
  slotIndex: number;
  player1UserId?: number | null;
  player1Name?: string | null;
  player2UserId?: number | null;
  player2Name?: string | null;
  player1IsBye: boolean;
  player2IsBye: boolean;
  status: TournamentMatchStatus | string;
  winnerUserId?: number | null;
  finishReason?: TournamentFinishReason | string | null;
  player1Correct: number;
  player2Correct: number;
}

export interface TournamentBracket {
  tournamentId: number;
  status: TournamentStatus | string;
  bracketSize: number;
  currentRoundNumber: number;
  matches: TournamentBracketMatch[];
  championUserId?: number | null;
  championName?: string | null;
}

export interface TournamentMatchView {
  matchId: number;
  tournamentId: number;
  tournamentName: string;
  roundNumber: number;
  roundName: string;
  status: TournamentMatchStatus | string;
  player1UserId?: number | null;
  player1Name: string;
  player2UserId?: number | null;
  player2Name: string;
  player1Correct: number;
  player2Correct: number;
  player1TimeMs: number;
  player2TimeMs: number;
  currentQuestionIndex: number;
  currentMatchQuestionId?: number | null;
  totalQuestions: number;
  secondsPerQuestion: number;
  questionStartedAt?: string | null;
  secondsLeft?: number | null;
  currentQuestion?: PlayQuestion | null;
  amIPlayer: boolean;
  waitingOpponent: boolean;
  winnerUserId?: number | null;
  finishReason?: TournamentFinishReason | string | null;
  myAnswers: AnsweredQuestion[];
}

export interface TournamentAnswerRequest {
  matchQuestionId: number;
  optionId?: number | null;
}

export interface TournamentAnswerResult {
  correct: boolean;
  correctOptionId?: number | null;
  explanation?: string | null;
  state: TournamentMatchView;
  newBadges: Badge[];
}

export interface TournamentTrophy {
  id: number;
  tournamentId: number;
  tournamentName: string;
  obtainedAt: string;
  championResult: string;
}

export interface CompetitiveProfile {
  level: Level;
  generalRank?: number | null;
  trophyCount: number;
  achievementCount: number;
  tournamentsWon: number;
  tournamentsPlayed: number;
  matchWins: number;
  matchLosses: number;
  finalsReached: number;
  trophies: TournamentTrophy[];
  badges: Badge[];
}

/** Events the TournamentHub pushes to clients. */
export const TOURNAMENT_HUB_EVENTS = [
  'ParticipantJoined',
  'ParticipantDisconnected',
  'ParticipantReconnected',
  'LobbyUpdated',
  'TournamentStarted',
  'MatchAssigned',
  'MatchUpdated',
  'MatchFinished',
  'RoundStarted',
  'ChampionDeclared',
  'TournamentFinished',
  'AchievementUnlocked'
] as const;

export type TournamentHubEvent = (typeof TOURNAMENT_HUB_EVENTS)[number];

export interface TournamentHubHandle {
  connection: HubConnection;
  close(): Promise<void>;
}

export const TOURNAMENT_STATUS_LABELS: Record<string, string> = {
  registration_open: 'Inscripciones abiertas',
  registration_closed: 'Inscripciones cerradas',
  preparing: 'Preparando llaves',
  in_progress: 'En juego',
  finished: 'Finalizado',
  cancelled: 'Cancelado'
};

export function tournamentStatusLabel(status: string): string {
  return TOURNAMENT_STATUS_LABELS[status] ?? status;
}

export function tournamentStatusTone(status: string): 'good' | 'warn' | '' {
  if (status === 'registration_open' || status === 'in_progress') return 'good';
  if (status === 'registration_closed' || status === 'preparing' || status === 'cancelled') return 'warn';
  return '';
}

export const FINISH_REASON_LABELS: Record<string, string> = {
  score: 'por puntaje',
  time: 'por tiempo',
  sudden_death: 'en muerte súbita',
  walkover: 'por W.O.',
  bye: 'pase libre'
};

@Injectable({ providedIn: 'root' })
export class TournamentApi {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionStore);
  private readonly authApi = inject(AuthApi);
  private readonly base = `${env.apiUrl}/api/tournaments`;
  private hubAccessToken: string | null = null;

  list() {
    return this.http.get<TournamentListItem[]>(this.base);
  }

  create(body: CreateTournamentRequest) {
    return this.http.post<TournamentListItem>(this.base, body);
  }

  lobby(id: number) {
    return this.http.get<TournamentLobby>(`${this.base}/${id}`);
  }

  join(id: number) {
    return this.http.post<TournamentLobby>(`${this.base}/${id}/join`, {});
  }

  closeRegistration(id: number) {
    return this.http.post<void>(`${this.base}/${id}/close-registration`, {});
  }

  start(id: number) {
    return this.http.post<TournamentBracket>(`${this.base}/${id}/start`, {});
  }

  cancel(id: number) {
    return this.http.post<void>(`${this.base}/${id}/cancel`, {});
  }

  bracket(id: number) {
    return this.http.get<TournamentBracket>(`${this.base}/${id}/bracket`);
  }

  match(matchId: number) {
    return this.http.get<TournamentMatchView>(`${this.base}/matches/${matchId}`);
  }

  answer(matchId: number, matchQuestionId: number, optionId: number | null) {
    const body: TournamentAnswerRequest = { matchQuestionId, optionId };
    return this.http.post<TournamentAnswerResult>(`${this.base}/matches/${matchId}/answer`, body);
  }

  trophies() {
    return this.http.get<TournamentTrophy[]>(`${this.base}/me/trophies`);
  }

  profile() {
    return this.http.get<CompetitiveProfile>(`${this.base}/me/profile`);
  }

  // ---- SignalR (/hubs/tournaments) ----

  hubUrl(): string {
    const root = env.apiUrl || (typeof location !== 'undefined' ? location.origin : '');
    return `${root}/hubs/tournaments`;
  }

  buildHub(): HubConnection {
    return new HubConnectionBuilder()
      .withUrl(this.hubUrl(), {
        withCredentials: true,
        accessTokenFactory: () => this.resolveHubAccessToken()
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (ctx) => Math.min(10_000, 1000 * 2 ** Math.min(ctx.previousRetryCount, 4))
      })
      .configureLogging(LogLevel.Warning)
      .build();
  }

  /**
   * Connects, joins the tournament group (JoinTournament) and forwards every hub event to `onEvent`.
   * Re-joins automatically after a reconnect.
   */
  async watch(
    tournamentId: number,
    onEvent: (event: TournamentHubEvent, payload: unknown) => void
  ): Promise<TournamentHubHandle> {
    const hub = this.buildHub();
    for (const name of TOURNAMENT_HUB_EVENTS) {
      hub.on(name, (payload: unknown) => onEvent(name, payload));
    }
    hub.onreconnected(() => {
      hub.invoke('JoinTournament', tournamentId).catch(() => undefined);
    });

    try {
      await this.ensureJoined(hub, tournamentId);
    } catch (err) {
      await hub.stop().catch(() => undefined);
      throw err;
    }

    return {
      connection: hub,
      close: async () => {
        try {
          if (hub.state === 'Connected') {
            await hub.invoke('LeaveTournament', tournamentId);
          }
        } catch {
          /* the server also drops the group when the socket closes */
        }
        await hub.stop().catch(() => undefined);
      }
    };
  }

  private async ensureJoined(hub: HubConnection, tournamentId: number): Promise<void> {
    this.hubAccessToken = null;
    if (hub.state === 'Disconnected') {
      await hub.start();
    }
    try {
      await hub.invoke('JoinTournament', tournamentId);
    } catch {
      this.hubAccessToken = null;
      try {
        await firstValueFrom(this.session.refreshSession());
      } catch {
        /* cookie may already be valid */
      }
      if (hub.state === 'Disconnected') {
        await hub.start();
      }
      await hub.invoke('JoinTournament', tournamentId);
    }
  }

  private async resolveHubAccessToken(): Promise<string> {
    const bearer = this.session.token();
    if (bearer) {
      return bearer;
    }
    if (this.hubAccessToken) {
      return this.hubAccessToken;
    }
    try {
      const res = await firstValueFrom(this.authApi.hubAccess());
      this.hubAccessToken = res.token || null;
      return this.hubAccessToken ?? '';
    } catch {
      return '';
    }
  }
}
