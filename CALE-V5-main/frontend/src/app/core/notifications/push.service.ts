import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { env } from '../config/env';

export type PushPermission = NotificationPermission | 'unsupported';

const DISMISS_KEY = 'cale.push.dismissedAt';
const DISMISS_DAYS = 7;

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

  get dismissedRecently(): boolean {
    try {
      const at = Number(localStorage.getItem(DISMISS_KEY) || 0);
      return at > 0 && Date.now() - at < DISMISS_DAYS * 86400000;
    } catch {
      return false;
    }
  }

  dismiss(): void {
    try {
      localStorage.setItem(DISMISS_KEY, String(Date.now()));
    } catch {
      // Storage may be unavailable.
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
      await this.subscribe();
      await firstValueFrom(this.http.post(`${this.base}/test`, {}));
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
    if (this.permission() !== 'granted') return;
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
