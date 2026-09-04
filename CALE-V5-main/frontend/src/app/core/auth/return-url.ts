const RETURN_URL_KEY = 'cale.auth.returnUrl';

/** Safe in-app paths we may restore after login. */
export function isSafeReturnUrl(url: string | null | undefined): url is string {
  return !!url
    && url.startsWith('/')
    && !url.startsWith('//')
    && !url.toLowerCase().startsWith('/login')
    && !url.toLowerCase().startsWith('/register');
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
  return isSafeReturnUrl(raw) ? raw : null;
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
