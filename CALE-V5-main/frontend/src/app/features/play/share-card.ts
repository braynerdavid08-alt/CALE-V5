export interface ShareCardData {
  /** Small caption above the headline, e.g. "Simulacro CALE". */
  kicker: string;
  /** Big value, e.g. "92%" or "🔥 7 días". */
  headline: string;
  /** Line under the headline, e.g. "¡Aprobé el simulacro!". */
  title: string;
  /** Optional detail lines. */
  details?: string[];
  /** Student display name. */
  name?: string | null;
  tone?: 'success' | 'primary' | 'warning';
}

const WIDTH = 1080;
const HEIGHT = 1350;

const TONES: Record<NonNullable<ShareCardData['tone']>, [string, string]> = {
  success: ['#0f7a4d', '#16a34a'],
  primary: ['#0f1712', '#15803d'],
  warning: ['#9a3412', '#f59e0b']
};

export async function renderShareCard(data: ShareCardData): Promise<Blob> {
  const canvas = document.createElement('canvas');
  canvas.width = WIDTH;
  canvas.height = HEIGHT;
  const g = canvas.getContext('2d');
  if (!g) throw new Error('canvas_unavailable');

  const [from, to] = TONES[data.tone ?? 'primary'];
  const bg = g.createLinearGradient(0, 0, WIDTH, HEIGHT);
  bg.addColorStop(0, from);
  bg.addColorStop(1, to);
  g.fillStyle = bg;
  g.fillRect(0, 0, WIDTH, HEIGHT);

  g.fillStyle = 'rgba(255,255,255,0.08)';
  g.beginPath();
  g.arc(WIDTH - 120, 160, 260, 0, Math.PI * 2);
  g.fill();
  g.beginPath();
  g.arc(90, HEIGHT - 140, 200, 0, Math.PI * 2);
  g.fill();

  g.fillStyle = '#ffffff';
  g.textAlign = 'center';
  g.textBaseline = 'alphabetic';

  g.font = '700 44px system-ui, "Segoe UI", sans-serif';
  g.fillText('Luz Verde', WIDTH / 2, 150);

  g.globalAlpha = 0.85;
  g.font = '600 40px system-ui, "Segoe UI", sans-serif';
  g.fillText(data.kicker.toUpperCase(), WIDTH / 2, 380);
  g.globalAlpha = 1;

  g.font = `800 ${fitSize(g, data.headline, 220, WIDTH - 140)}px system-ui, "Segoe UI", sans-serif`;
  g.fillText(data.headline, WIDTH / 2, 620);

  g.font = '700 64px system-ui, "Segoe UI", sans-serif';
  wrap(g, data.title, WIDTH - 160).forEach((line, i) => g.fillText(line, WIDTH / 2, 760 + i * 78));

  g.globalAlpha = 0.9;
  g.font = '500 40px system-ui, "Segoe UI", sans-serif';
  (data.details ?? []).slice(0, 3).forEach((line, i) => g.fillText(line, WIDTH / 2, 960 + i * 56));
  g.globalAlpha = 1;

  if (data.name) {
    g.font = '600 42px system-ui, "Segoe UI", sans-serif';
    g.fillText(data.name, WIDTH / 2, HEIGHT - 190);
  }
  g.globalAlpha = 0.8;
  g.font = '500 34px system-ui, "Segoe UI", sans-serif';
  g.fillText('Preparándome para el examen teórico CALE', WIDTH / 2, HEIGHT - 120);
  g.globalAlpha = 1;

  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('share_render_failed'))), 'image/png')
  );
}

/** Shares the card with the native share sheet, or downloads it when sharing files is not supported. */
export async function shareCard(data: ShareCardData): Promise<'shared' | 'downloaded' | 'cancelled'> {
  const blob = await renderShareCard(data);
  const file = new File([blob], 'mi-cale-logro.png', { type: 'image/png' });
  const text = `${data.title} · ${data.headline} — Luz Verde`;

  const nav = navigator as Navigator & { canShare?: (d: ShareData) => boolean };
  if (nav.share && nav.canShare?.({ files: [file] })) {
    try {
      await nav.share({ files: [file], title: 'Luz Verde', text });
      return 'shared';
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') return 'cancelled';
    }
  }

  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = file.name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 2000);
  return 'downloaded';
}

function fitSize(g: CanvasRenderingContext2D, text: string, max: number, width: number): number {
  let size = max;
  while (size > 60) {
    g.font = `800 ${size}px system-ui, "Segoe UI", sans-serif`;
    if (g.measureText(text).width <= width) break;
    size -= 10;
  }
  return size;
}

function wrap(g: CanvasRenderingContext2D, text: string, width: number): string[] {
  const words = text.split(/\s+/);
  const lines: string[] = [];
  let line = '';
  for (const word of words) {
    const next = line ? `${line} ${word}` : word;
    if (g.measureText(next).width > width && line) {
      lines.push(line);
      line = word;
    } else {
      line = next;
    }
  }
  if (line) lines.push(line);
  return lines.slice(0, 2);
}
