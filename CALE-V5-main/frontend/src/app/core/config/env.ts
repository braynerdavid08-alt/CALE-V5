/**
 * API base URL.
 * - Empty string = same origin (ng serve + proxy, or SPA hosted by Cale.Api).
 * - Override at runtime via /config.js → window.__CALE_CONFIG__.apiUrl
 * - Or set before bootstrap: window.__CALE_CONFIG__ = { apiUrl: 'https://api.tudominio.com' }
 */
declare global {
  interface Window {
    __CALE_CONFIG__?: { apiUrl?: string };
  }
}

function readApiUrl(): string {
  try {
    const fromWindow = globalThis.window?.__CALE_CONFIG__?.apiUrl;
    if (fromWindow !== undefined && fromWindow !== null) {
      return String(fromWindow).replace(/\/$/, '');
    }
  } catch {
    /* SSR / non-browser */
  }
  return '';
}

export const env = {
  get apiUrl(): string {
    return readApiUrl();
  }
};
