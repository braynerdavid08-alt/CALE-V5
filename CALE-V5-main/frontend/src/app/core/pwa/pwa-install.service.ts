import { Injectable, signal } from '@angular/core';

/** Chromium `beforeinstallprompt` (not available on iOS Safari/Chrome). */
export interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

export type PwaInstallMode =
  /** App already running from home screen / installed window. */
  | 'installed'
  /** Native install dialog can be shown (Android / desktop Chromium). */
  | 'native'
  /** Manual “Add to Home Screen” steps (iPhone / iPad). */
  | 'ios-guide'
  /** No deferred prompt yet; show a short generic tip. */
  | 'manual'
  /** Nothing useful to offer (e.g. unsupported context). */
  | 'hidden';

/**
 * Captures the install prompt and exposes whether Luz Verde can be added
 * to the home screen. Does not touch auth, routes, or the existing service worker.
 */
@Injectable({ providedIn: 'root' })
export class PwaInstallService {
  private deferred: BeforeInstallPromptEvent | null = null;

  /** True when running as an installed / home-screen app. */
  readonly installed = signal(isStandaloneDisplay());
  /** True when a deferred `beforeinstallprompt` is ready. */
  readonly canNativeInstall = signal(false);
  readonly iosDevice = signal(isAppleTouchDevice());

  constructor() {
    if (typeof window === 'undefined') {
      return;
    }

    window.addEventListener('beforeinstallprompt', (event) => {
      event.preventDefault();
      this.deferred = event as BeforeInstallPromptEvent;
      this.canNativeInstall.set(true);
      this.installed.set(isStandaloneDisplay());
    });

    window.addEventListener('appinstalled', () => {
      this.deferred = null;
      this.canNativeInstall.set(false);
      this.installed.set(true);
    });

    // Re-check after returning from the share sheet (iOS) or install UI.
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') {
        this.refresh();
      }
    });
  }

  refresh(): void {
    this.installed.set(isStandaloneDisplay());
    if (this.installed()) {
      this.deferred = null;
      this.canNativeInstall.set(false);
    }
  }

  mode(): PwaInstallMode {
    this.refresh();
    if (this.installed()) {
      return 'installed';
    }
    if (this.iosDevice()) {
      return 'ios-guide';
    }
    if (this.canNativeInstall() && this.deferred) {
      return 'native';
    }
    if (typeof window !== 'undefined' && 'serviceWorker' in navigator) {
      return 'manual';
    }
    return 'hidden';
  }

  /** Offer the install CTA (button) for any mode except hidden. Installed shows status only. */
  shouldShowOffer(): boolean {
    const m = this.mode();
    return m === 'native' || m === 'ios-guide' || m === 'manual' || m === 'installed';
  }

  /** Compact topbar / public CTA — hide when already installed. */
  shouldShowInstallCta(): boolean {
    const m = this.mode();
    return m === 'native' || m === 'ios-guide' || m === 'manual';
  }

  /**
   * Triggers the browser install UI when available.
   * @returns accepted | dismissed | unavailable
   */
  async promptNativeInstall(): Promise<'accepted' | 'dismissed' | 'unavailable'> {
    if (!this.deferred) {
      return 'unavailable';
    }
    const event = this.deferred;
    try {
      await event.prompt();
      const { outcome } = await event.userChoice;
      this.deferred = null;
      this.canNativeInstall.set(false);
      if (outcome === 'accepted') {
        this.installed.set(true);
      }
      return outcome;
    } catch {
      this.deferred = null;
      this.canNativeInstall.set(false);
      return 'unavailable';
    }
  }
}

export function isStandaloneDisplay(): boolean {
  if (typeof window === 'undefined') {
    return false;
  }
  const mqStandalone = window.matchMedia?.('(display-mode: standalone)').matches === true;
  const mqMinimal = window.matchMedia?.('(display-mode: minimal-ui)').matches === true;
  const iosStandalone =
    (navigator as Navigator & { standalone?: boolean }).standalone === true;
  // Some Android launchers set this referrer when opened from the home screen icon.
  const androidTwa = document.referrer.startsWith('android-app://');
  return mqStandalone || mqMinimal || iosStandalone || androidTwa;
}

/** iPhone, iPod, iPad (incl. iPadOS desktop UA). */
export function isAppleTouchDevice(): boolean {
  if (typeof navigator === 'undefined') {
    return false;
  }
  const ua = navigator.userAgent || '';
  if (/iphone|ipod|ipad/i.test(ua)) {
    return true;
  }
  // iPadOS 13+ may report as Macintosh with touch.
  return navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1;
}
