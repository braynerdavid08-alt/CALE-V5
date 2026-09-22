import { Injectable, signal } from '@angular/core';
import { CreateGameShowBody, GameShowRoundInput } from '../api/game-show.api';

const DRAFT_KEY = 'cale.game-show.draft.v1';

export interface GameShowDraft {
  title: string;
  teamAName: string;
  teamBName: string;
  rounds: GameShowRoundInput[];
  updatedAt: string;
}

function emptyRound(): GameShowRoundInput {
  return {
    questionText: '',
    answers: [
      { text: '', points: 30, aliases: [] },
      { text: '', points: 25, aliases: [] },
      { text: '', points: 20, aliases: [] },
      { text: '', points: 15, aliases: [] },
      { text: '', points: 10, aliases: [] }
    ]
  };
}

function defaultDraft(): GameShowDraft {
  return {
    title: '100 Estudiantes Dijeron',
    teamAName: 'Equipo A',
    teamBName: 'Equipo B',
    rounds: [emptyRound()],
    updatedAt: new Date().toISOString()
  };
}

@Injectable({ providedIn: 'root' })
export class GameShowDraftStore {
  readonly draft = signal<GameShowDraft>(this.read());

  emptyRound(): GameShowRoundInput {
    return emptyRound();
  }

  patch(partial: Partial<Omit<GameShowDraft, 'updatedAt'>>): void {
    const next: GameShowDraft = {
      ...this.draft(),
      ...partial,
      updatedAt: new Date().toISOString()
    };
    this.draft.set(next);
    this.write(next);
  }

  setRounds(rounds: GameShowRoundInput[]): void {
    this.patch({ rounds: rounds.length ? rounds : [emptyRound()] });
  }

  setRoom(title: string, teamAName: string, teamBName: string): void {
    this.patch({ title, teamAName, teamBName });
  }

  applyPack(body: CreateGameShowBody): void {
    this.patch({
      title: body.title || this.draft().title,
      teamAName: body.teamAName || this.draft().teamAName,
      teamBName: body.teamBName || this.draft().teamBName,
      rounds: (body.rounds || []).map((r) => ({
        questionText: r.questionText || '',
        sourceQuestionId: r.sourceQuestionId ?? null,
        answers: (r.answers || []).map((a) => ({
          text: a.text || '',
          points: a.points || 1,
          aliases: [...(a.aliases || [])]
        }))
      }))
    });
  }

  toCreateBody(): CreateGameShowBody {
    const d = this.draft();
    return {
      title: d.title,
      teamAName: d.teamAName,
      teamBName: d.teamBName,
      rounds: d.rounds
    };
  }

  readyRoundCount(): number {
    return this.draft().rounds.filter((r) =>
      (r.questionText || '').trim().length >= 5
      && (r.answers || []).filter((a) => (a.text || '').trim().length > 0).length === 5
    ).length;
  }

  private read(): GameShowDraft {
    try {
      const raw = localStorage.getItem(DRAFT_KEY);
      if (!raw) return defaultDraft();
      const parsed = JSON.parse(raw) as GameShowDraft;
      if (!parsed?.rounds?.length) return defaultDraft();
      return {
        title: parsed.title || '100 Estudiantes Dijeron',
        teamAName: parsed.teamAName || 'Equipo A',
        teamBName: parsed.teamBName || 'Equipo B',
        rounds: parsed.rounds,
        updatedAt: parsed.updatedAt || new Date().toISOString()
      };
    } catch {
      return defaultDraft();
    }
  }

  private write(draft: GameShowDraft): void {
    try {
      localStorage.setItem(DRAFT_KEY, JSON.stringify(draft));
    } catch {
      // ignore quota / private mode
    }
  }
}
