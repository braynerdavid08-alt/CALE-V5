import { renderSVG } from 'uqr';

/** Build a data URL for a QR code image (ESM, no CommonJS). */
export function buildQrDataUrl(text: string, size = 240): string {
  const pixelSize = Math.max(4, Math.floor(size / 25));
  const svg = renderSVG(text, {
    ecc: 'M',
    border: 1,
    pixelSize,
    blackColor: '#000000',
    whiteColor: '#ffffff'
  });
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}
