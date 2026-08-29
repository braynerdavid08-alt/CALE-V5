/** Extract a live room join code from a QR payload or manual input. */
export function parseLiveJoinCode(raw: string): string | null {
  const text = raw.trim();
  if (!text) {
    return null;
  }

  const fromUrl = text.match(/\/live\/join\/([A-Za-z0-9]{4,12})/i);
  if (fromUrl?.[1]) {
    return fromUrl[1].toUpperCase();
  }

  const compact = text.replace(/\s+/g, '').toUpperCase();
  if (/^[A-Z0-9]{4,12}$/.test(compact)) {
    return compact;
  }

  return null;
}
