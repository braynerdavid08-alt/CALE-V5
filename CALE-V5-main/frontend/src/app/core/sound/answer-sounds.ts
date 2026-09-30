const SOUND_URLS = {
  correct: 'sounds/correct.mp3',
  wrong: 'sounds/wrong.mp3'
} as const;

export type AnswerSound = keyof typeof SOUND_URLS;

const loaded = new Map<AnswerSound, HTMLAudioElement>();

function source(kind: AnswerSound): HTMLAudioElement {
  let audio = loaded.get(kind);
  if (!audio) {
    audio = new Audio(SOUND_URLS[kind]);
    audio.preload = 'auto';
    loaded.set(kind, audio);
  }
  return audio;
}

/** Downloads the answer sounds ahead of time so the first answer plays instantly. */
export function preloadAnswerSounds(): void {
  if (typeof Audio === 'undefined') return;
  (Object.keys(SOUND_URLS) as AnswerSound[]).forEach((kind) => source(kind).load());
}

/**
 * Plays the app's correct / wrong audio file. Each call uses a fresh copy so
 * quick consecutive answers overlap instead of cutting each other off.
 * `fallback` runs when the browser can't play the file (blocked or offline).
 */
export function playAnswerSound(kind: AnswerSound, volume = 0.9, fallback?: () => void): void {
  if (typeof Audio === 'undefined') {
    fallback?.();
    return;
  }
  try {
    const audio = source(kind).cloneNode(true) as HTMLAudioElement;
    audio.volume = volume;
    void audio.play().catch(() => fallback?.());
  } catch {
    fallback?.();
  }
}
