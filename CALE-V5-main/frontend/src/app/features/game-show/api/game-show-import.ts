import { CreateGameShowBody, GameShowAnswerInput, GameShowRoundInput } from './game-show.api';

export class GameShowImportError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'GameShowImportError';
  }
}

/** Parse exported GameShow JSON or CSV into a create body for the draft form. */
export function parseGameShowImport(text: string, fileName: string): CreateGameShowBody {
  const raw = stripBom((text || '').trim());
  if (!raw) {
    throw new GameShowImportError('El archivo está vacío.');
  }

  const lower = (fileName || '').toLowerCase();
  if (lower.endsWith('.json') || raw.startsWith('{') || raw.startsWith('[')) {
    return parseJson(raw);
  }
  if (lower.endsWith('.csv') || looksLikeCsv(raw)) {
    return parseCsv(raw);
  }

  throw new GameShowImportError('Usa un archivo .json o .csv exportado desde CALE.');
}

function parseJson(raw: string): CreateGameShowBody {
  let data: unknown;
  try {
    data = JSON.parse(raw);
  } catch {
    throw new GameShowImportError('JSON inválido.');
  }

  if (!data || typeof data !== 'object' || Array.isArray(data)) {
    throw new GameShowImportError('El JSON debe ser un objeto con title, teamAName, teamBName y rounds.');
  }

  const obj = data as Record<string, unknown>;
  const roundsRaw = obj['rounds'];
  if (!Array.isArray(roundsRaw) || roundsRaw.length < 1) {
    throw new GameShowImportError('El JSON no tiene rondas.');
  }
  if (roundsRaw.length > 20) {
    throw new GameShowImportError('Máximo 20 rondas por partida.');
  }

  const rounds: GameShowRoundInput[] = roundsRaw.map((r, i) => mapRound(r, i + 1));
  return {
    title: str(obj['title']) || '100 Estudiantes Dijeron',
    teamAName: str(obj['teamAName']) || 'Equipo A',
    teamBName: str(obj['teamBName']) || 'Equipo B',
    rounds
  };
}

function mapRound(raw: unknown, roundNo: number): GameShowRoundInput {
  if (!raw || typeof raw !== 'object') {
    throw new GameShowImportError(`Ronda ${roundNo}: formato inválido.`);
  }
  const r = raw as Record<string, unknown>;
  const answersRaw = r['answers'];
  if (!Array.isArray(answersRaw) || answersRaw.length < 1) {
    throw new GameShowImportError(`Ronda ${roundNo}: faltan respuestas.`);
  }

  const answers = answersRaw
    .map((a) => mapAnswer(a))
    .sort((a, b) => b.points - a.points);

  while (answers.length < 5) {
    answers.push({ text: '', points: Math.max(1, 30 - answers.length * 5), aliases: [] });
  }

  return {
    questionText: str(r['questionText']),
    sourceQuestionId: numOrNull(r['sourceQuestionId']),
    answers: answers.slice(0, 5)
  };
}

function mapAnswer(raw: unknown): GameShowAnswerInput {
  if (!raw || typeof raw !== 'object') {
    return { text: '', points: 1, aliases: [] };
  }
  const a = raw as Record<string, unknown>;
  const aliases = Array.isArray(a['aliases'])
    ? a['aliases'].map((x) => String(x ?? '').trim()).filter(Boolean)
    : [];
  return {
    text: str(a['text']),
    points: Math.max(1, Math.floor(num(a['points'], 1))),
    aliases
  };
}

function parseCsv(raw: string): CreateGameShowBody {
  const rows = parseCsvRows(raw);
  if (rows.length < 2) {
    throw new GameShowImportError('El CSV no tiene filas de datos.');
  }

  const header = rows[0].map((h) => h.trim().toLowerCase());
  const idx = {
    ronda: col(header, ['ronda', 'round']),
    pregunta: col(header, ['pregunta', 'question', 'questiontext']),
    rank: col(header, ['rank', 'orden', 'posicion', 'posición']),
    respuesta: col(header, ['respuesta', 'answer', 'text']),
    puntos: col(header, ['puntos', 'points', 'score']),
    aliases: col(header, ['aliases', 'alias', 'sinonimos', 'sinónimos'])
  };

  if (idx.ronda < 0 || idx.pregunta < 0 || idx.respuesta < 0 || idx.puntos < 0) {
    throw new GameShowImportError(
      'Cabecera CSV inválida. Esperado: ronda,pregunta,rank,respuesta,puntos,aliases'
    );
  }

  type Acc = { questionText: string; byRank: Map<number, GameShowAnswerInput> };
  const byRound = new Map<number, Acc>();

  for (let i = 1; i < rows.length; i++) {
    const row = rows[i];
    if (row.every((c) => !c.trim())) continue;

    const roundNo = Math.floor(num(row[idx.ronda], 0));
    if (roundNo < 1) {
      throw new GameShowImportError(`Fila ${i + 1}: número de ronda inválido.`);
    }

    const question = (row[idx.pregunta] ?? '').trim();
    const answerText = (row[idx.respuesta] ?? '').trim();
    const points = Math.max(1, Math.floor(num(row[idx.puntos], 1)));
    const fallbackRank = (byRound.get(roundNo)?.byRank.size ?? 0) + 1;
    const rank = idx.rank >= 0 ? Math.floor(num(row[idx.rank], fallbackRank)) : 0;
    const aliases =
      idx.aliases >= 0
        ? (row[idx.aliases] ?? '')
            .split('|')
            .map((x) => x.trim())
            .filter(Boolean)
        : [];

    let acc = byRound.get(roundNo);
    if (!acc) {
      acc = { questionText: question, byRank: new Map() };
      byRound.set(roundNo, acc);
    } else if (question && !acc.questionText) {
      acc.questionText = question;
    }

    const key = rank > 0 ? rank : acc.byRank.size + 1;
    acc.byRank.set(key, { text: answerText, points, aliases });
  }

  if (byRound.size < 1) {
    throw new GameShowImportError('No se encontraron rondas en el CSV.');
  }
  if (byRound.size > 20) {
    throw new GameShowImportError('Máximo 20 rondas por partida.');
  }

  const rounds: GameShowRoundInput[] = [...byRound.entries()]
    .sort((a, b) => a[0] - b[0])
    .map(([, acc]) => {
      const ranked = [...acc.byRank.entries()]
        .sort((a, b) => a[0] - b[0])
        .map(([, ans]) => ans);
      while (ranked.length < 5) {
        ranked.push({ text: '', points: Math.max(1, 30 - ranked.length * 5), aliases: [] });
      }
      return {
        questionText: acc.questionText,
        answers: ranked.slice(0, 5)
      };
    });

  return {
    title: '100 Estudiantes Dijeron',
    teamAName: 'Equipo A',
    teamBName: 'Equipo B',
    rounds
  };
}

function parseCsvRows(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = '';
  let inQuotes = false;

  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (inQuotes) {
      if (ch === '"') {
        if (text[i + 1] === '"') {
          cell += '"';
          i++;
        } else {
          inQuotes = false;
        }
      } else {
        cell += ch;
      }
      continue;
    }

    if (ch === '"') {
      inQuotes = true;
    } else if (ch === ',') {
      row.push(cell);
      cell = '';
    } else if (ch === '\n') {
      row.push(cell);
      rows.push(row);
      row = [];
      cell = '';
    } else if (ch === '\r') {
      // ignore; handle \r\n via \n
    } else {
      cell += ch;
    }
  }

  if (cell.length || row.length) {
    row.push(cell);
    rows.push(row);
  }

  return rows;
}

function col(header: string[], names: string[]): number {
  for (const n of names) {
    const i = header.indexOf(n);
    if (i >= 0) return i;
  }
  return -1;
}

function looksLikeCsv(raw: string): boolean {
  const first = raw.split(/\r?\n/, 1)[0]?.toLowerCase() ?? '';
  return first.includes('ronda') && first.includes('pregunta') && first.includes('respuesta');
}

function stripBom(s: string): string {
  return s.charCodeAt(0) === 0xfeff ? s.slice(1) : s;
}

function str(v: unknown): string {
  return typeof v === 'string' ? v.trim() : v == null ? '' : String(v).trim();
}

function num(v: unknown, fallback: number): number {
  if (typeof v === 'number' && Number.isFinite(v)) return v;
  if (typeof v === 'string' && v.trim()) {
    const n = Number(v.trim().replace(',', '.'));
    if (Number.isFinite(n)) return n;
  }
  return fallback;
}

function numOrNull(v: unknown): number | null {
  if (v == null || v === '') return null;
  const n = num(v, NaN);
  return Number.isFinite(n) ? Math.floor(n) : null;
}
