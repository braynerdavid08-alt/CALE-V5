const PRESETS_KEY = 'cale.live.hub.presets';
const DRAFT_KEY = 'cale.live.hub.draft';

export type LiveDistributionMode = 'mix' | 'quotas';

export interface LiveHubDraft {
  title: string;
  mode: string;
  questionCount: number;
  secondsPerQuestion: number;
  randomize: boolean;
  shuffleOptions: boolean;
  distributionMode: LiveDistributionMode;
  selectedBankIds: number[];
  selectedThemesByBank: Record<number, string[]>;
  bankQuotas: Record<number, number>;
  selectedDifficulties: string[];
  presentationId?: number | null;
}

export interface LiveHubPreset extends LiveHubDraft {
  id: string;
  name: string;
  savedAt: string;
}

function readJson<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) {
      return fallback;
    }
    return JSON.parse(raw) as T;
  } catch {
    return fallback;
  }
}

function writeJson(key: string, value: unknown): void {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    /* quota / private mode */
  }
}

export function loadLiveHubDraft(): LiveHubDraft | null {
  return readJson<LiveHubDraft | null>(DRAFT_KEY, null);
}

export function saveLiveHubDraft(draft: LiveHubDraft): void {
  writeJson(DRAFT_KEY, draft);
}

export function loadLiveHubPresets(): LiveHubPreset[] {
  return readJson<LiveHubPreset[]>(PRESETS_KEY, []);
}

export function saveLiveHubPreset(preset: LiveHubPreset): void {
  const items = loadLiveHubPresets().filter((p) => p.id !== preset.id);
  items.unshift(preset);
  writeJson(PRESETS_KEY, items.slice(0, 12));
}

export function deleteLiveHubPreset(id: string): void {
  writeJson(
    PRESETS_KEY,
    loadLiveHubPresets().filter((p) => p.id !== id)
  );
}
