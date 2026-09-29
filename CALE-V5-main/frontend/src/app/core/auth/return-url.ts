const RETURN_URL_KEY = 'cale.auth.returnUrl';

/** Marketing / guest pages — never restore these after login. */
const PUBLIC_PATH_PREFIXES = [
  '/',
  '/nosotros',
  '/cursos',
  '/escuelas',
  '/instructores',
  '/blog',
  '/contacto',
  '/verify-email',
  '/login',
  '/register'
];

function pathOnly(url: string): string {
  return url.split('?')[0].split('#')[0] || '/';
}

function isPublicMarketingPath(url: string): boolean {
  const path = pathOnly(url).toLowerCase();
  if (path === '/') {
    return true;
  }
  return PUBLIC_PATH_PREFIXES.some(
    (p) => p !== '/' && (path === p || path.startsWith(`${p}/`))
  );
}

/** Safe in-app paths we may restore after login (authenticated app areas only). */
export function isSafeReturnUrl(url: string | null | undefined): url is string {
  return !!url
    && url.startsWith('/')
    && !url.startsWith('//')
    && !isPublicMarketingPath(url);
}

export function stashReturnUrl(url: string | null | undefined): void {
  if (!isSafeReturnUrl(url) || typeof sessionStorage === 'undefined') {
    return;
  }
  sessionStorage.setItem(RETURN_URL_KEY, url);
}

export function peekReturnUrl(): string | null {
  if (typeof sessionStorage === 'undefined') {
    return null;
  }
  const raw = sessionStorage.getItem(RETURN_URL_KEY);
  if (!isSafeReturnUrl(raw)) {
    sessionStorage.removeItem(RETURN_URL_KEY);
    return null;
  }
  return raw;
}

export function takeReturnUrl(preferred?: string | null): string | null {
  const fromPreferred = isSafeReturnUrl(preferred) ? preferred : null;
  const fromStore = peekReturnUrl();
  const target = fromPreferred ?? fromStore;
  if (typeof sessionStorage !== 'undefined') {
    sessionStorage.removeItem(RETURN_URL_KEY);
  }
  return target;
}

export function clearReturnUrl(): void {
  if (typeof sessionStorage !== 'undefined') {
    sessionStorage.removeItem(RETURN_URL_KEY);
  }
}
