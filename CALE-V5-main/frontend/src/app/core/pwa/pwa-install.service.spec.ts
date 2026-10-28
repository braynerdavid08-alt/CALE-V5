import { isAppleTouchDevice, isStandaloneDisplay } from './pwa-install.service';

describe('PWA install detection', () => {
  const originalNavigator = globalThis.navigator;
  const originalMatchMedia = window.matchMedia;
  const originalReferrer = Object.getOwnPropertyDescriptor(Document.prototype, 'referrer');

  afterEach(() => {
    Object.defineProperty(globalThis, 'navigator', {
      configurable: true,
      value: originalNavigator
    });
    window.matchMedia = originalMatchMedia;
    if (originalReferrer) {
      Object.defineProperty(Document.prototype, 'referrer', originalReferrer);
    }
  });

  it('detects iPhone from userAgent', () => {
    Object.defineProperty(globalThis, 'navigator', {
      configurable: true,
      value: {
        userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X)',
        platform: 'iPhone',
        maxTouchPoints: 5
      }
    });
    expect(isAppleTouchDevice()).toBe(true);
  });

  it('detects iPadOS desktop UA (MacIntel + touch)', () => {
    Object.defineProperty(globalThis, 'navigator', {
      configurable: true,
      value: {
        userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 14_0)',
        platform: 'MacIntel',
        maxTouchPoints: 5
      }
    });
    expect(isAppleTouchDevice()).toBe(true);
  });

  it('does not treat a desktop Mac without touch as iOS', () => {
    Object.defineProperty(globalThis, 'navigator', {
      configurable: true,
      value: {
        userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 14_0)',
        platform: 'MacIntel',
        maxTouchPoints: 0
      }
    });
    expect(isAppleTouchDevice()).toBe(false);
  });

  it('detects standalone display-mode', () => {
    window.matchMedia = ((query: string) =>
      ({
        matches: query.includes('display-mode: standalone'),
        media: query,
        onchange: null,
        addListener: () => undefined,
        removeListener: () => undefined,
        addEventListener: () => undefined,
        removeEventListener: () => undefined,
        dispatchEvent: () => false
      })) as typeof window.matchMedia;
    Object.defineProperty(globalThis, 'navigator', {
      configurable: true,
      value: { userAgent: 'Chrome', platform: 'Win32', maxTouchPoints: 0, standalone: false }
    });
    Object.defineProperty(Document.prototype, 'referrer', {
      configurable: true,
      get: () => ''
    });
    expect(isStandaloneDisplay()).toBe(true);
  });

  it('detects iOS navigator.standalone', () => {
    window.matchMedia = ((query: string) =>
      ({
        matches: false,
        media: query,
        onchange: null,
        addListener: () => undefined,
        removeListener: () => undefined,
        addEventListener: () => undefined,
        removeEventListener: () => undefined,
        dispatchEvent: () => false
      })) as typeof window.matchMedia;
    Object.defineProperty(globalThis, 'navigator', {
      configurable: true,
      value: {
        userAgent: 'iPhone',
        platform: 'iPhone',
        maxTouchPoints: 5,
        standalone: true
      }
    });
    Object.defineProperty(Document.prototype, 'referrer', {
      configurable: true,
      get: () => ''
    });
    expect(isStandaloneDisplay()).toBe(true);
  });
});
