import { env } from '../config/env';

export function resolveMediaUrl(path: string | null | undefined): string {
  if (!path) {
    return '';
  }
  if (path.startsWith('http://') || path.startsWith('https://')) {
    return path;
  }

  const api = env.apiUrl.replace(/\/$/, '');
  return path.startsWith('/') ? `${api}${path}` : `${api}/${path}`;
}
