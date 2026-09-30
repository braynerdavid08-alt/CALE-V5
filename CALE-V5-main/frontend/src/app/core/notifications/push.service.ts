import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { env } from '../config/env';

export type PushPermission = NotificationPermission | 'unsupported';

const PROMPT_DONE_KEY = 'cale.push.promptDone';
const OPTED_OUT_KEY = 'cale.push.optedOut';

/** Web Push subscription for the installed app / browser (all roles). */
@Injectable({ providedIn: 'root' })
export class PushService {
  private readonly http = inject(HttpClient);
  private readonly base = `${env.apiUrl}/api/push`;

  readonly permission = signal<PushPermission>(this.readPermission());
  readonly busy = signal(false);

  get supported(): boolean {
    return typeof window !== 'undefined'
      && 'serviceWorker' in navigator
      && 'PushManager' in window
      && 'Notification' in window;
  }

  /** iPhone/iPad only allow push once the app is added to the home screen. */
  get needsInstallOnIos(): boolean {
    if (typeof navigator === 'undefined') return false;
    const ios = /iphone|ipad|ipod/i.test(navigator.userAgent)
      || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    return ios && !this.isStandalone();
  }

  /** The welcome popup is shown once per device; afterwards it lives in Mi perfil. */
  get promptDone(): boolean {
    return getFlag(PROMPT_DONE_KEY);
  }

  markPromptDone(): void {
    setFlag(PROMPT_DONE_KEY, true);
  }

  /** True when this browser holds an active push subscription. */
  async isSubscribedHere(): Promise<boolean> {
    if (!this.supported || this.readPermission() !== 'granted') return false;
    try {
      const reg = await navigator.serviceWorker.getRegistration();
      return !!(await reg?.pushManager.getSubscription());
    } catch {
      return false;
    }
  }

  async sendTest(): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base}/test`, {}));
  }

  /** Stops push on this device entirely (server link and browser subscription). */
  async disable(): Promise<void> {
    if (!this.supported) return;
    this.busy.set(true);
    setFlag(OPTED_OUT_KEY, true);
    try {
      await this.detach();
      const reg = await navigator.serviceWorker.getRegistration();
      const sub = await reg?.pushManager.getSubscription();
      await sub?.unsubscribe();
    } catch {
      // Best effort.
    } finally {
      this.busy.set(false);
    }
  }

  /** Asks for permission and registers this device. Returns true when push is active. */
  async enable(): Promise<boolean> {
    if (!this.supported) return false;
    this.busy.set(true);
    try {
      const result = await Notification.requestPermission();
      this.permission.set(result);
      if (result !== 'granted') return false;
      setFlag(OPTED_OUT_KEY, false);
      await this.subscribe();
      await this.sendTest();
      return true;
    } catch {
      return false;
    } finally {
      this.busy.set(false);
    }
  }

  /** Re-links this device to the signed-in user when permission was already granted. */
  async syncIfGranted(): Promise<void> {
    this.permission.set(this.readPermission());
    if (this.permission() !== 'granted' || getFlag(OPTED_OUT_KEY)) return;
    try {
      await this.subscribe();
    } catch {
      // Push service unreachable; polling still keeps the counter fresh.
    }
  }

  /** Stops sending this device pushes for the user that is logging out. */
  async detach(): Promise<void> {
    if (!this.supported) return;
    try {
      const reg = await navigator.serviceWorker.getRegistration();
      const sub = await reg?.pushManager.getSubscription();
      if (sub) {
        await firstValueFrom(this.http.post(`${this.base}/subscriptions/remove`, { endpoint: sub.endpoint }));
      }
    } catch {
      // Best effort.
    }
  }

  private async subscribe(): Promise<void> {
    const reg = (await navigator.serviceWorker.getRegistration())
      ?? (await navigator.serviceWorker.register('/sw.js'));
    await navigator.serviceWorker.ready;

    const { publicKey } = await firstValueFrom(
      this.http.get<{ publicKey: string }>(`${this.base}/public-key`)
    );
    const serverKey = urlBase64ToUint8Array(publicKey);

    let sub = await reg.pushManager.getSubscription();
    if (sub && !sameKey(sub.options.applicationServerKey, serverKey)) {
      await sub.unsubscribe();
      sub = null;
    }
    sub ??= await reg.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: serverKey
    });

    const json = sub.toJSON();
    await firstValueFrom(this.http.post(`${this.base}/subscriptions`, {
      endpoint: json.endpoint,
      keys: { p256dh: json.keys?.['p256dh'], auth: json.keys?.['auth'] }
    }));
  }

  private isStandalone(): boolean {
    return window.matchMedia?.('(display-mode: standalone)').matches
      || (navigator as Navigator & { standalone?: boolean }).standalone === true;
  }

  private readPermission(): PushPermission {
    if (typeof window === 'undefined' || !('Notification' in window)) return 'unsupported';
    return Notification.permission;
  }
}

function getFlag(key: string): boolean {
  try {
    return localStorage.getItem(key) === '1';
  } catch {
    return false;
  }
}

function setFlag(key: string, on: boolean): void {
  try {
    if (on) localStorage.setItem(key, '1');
    else localStorage.removeItem(key);
  } catch {
    // Storage may be unavailable.
  }
}

function urlBase64ToUint8Array(value: string): Uint8Array {
  const padding = '='.repeat((4 - (value.length % 4)) % 4);
  const base64 = (value + padding).replace(/-/g, '+').replace(/_/g, '/');
  const raw = atob(base64);
  const out = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
  return out;
}

function sameKey(current: ArrayBuffer | null, expected: Uint8Array): boolean {
  if (!current) return false;
  const a = new Uint8Array(current);
  if (a.length !== expected.length) return false;
  return a.every((b, i) => b === expected[i]);
}
