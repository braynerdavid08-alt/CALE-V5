import { encode } from 'uqr';

/**
 * Renders a QR code as a PNG data URL using Canvas (ESM-only, no CommonJS).
 * Matches the previous qrcode.toDataURL output for live session join links.
 */
export function buildQrDataUrl(text: string, size = 240): string {
  const trimmed = text.trim();
  if (!trimmed) {
    return '';
  }

  const qr = encode(trimmed, { ecc: 'M', border: 1 });
  const moduleCount = qr.size;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext('2d');
  if (!ctx) {
    throw new Error('No se pudo crear el lienzo para el código QR.');
  }

  const cell = size / moduleCount;
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, size, size);
  ctx.fillStyle = '#000000';
  for (let y = 0; y < moduleCount; y++) {
    const row = qr.data[y];
    for (let x = 0; x < moduleCount; x++) {
      if (row[x]) {
        ctx.fillRect(x * cell, y * cell, cell, cell);
      }
    }
  }

  return canvas.toDataURL('image/png');
}
